namespace AccessQueueServiceTests
{
    using global::AccessQueueService.Services;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    namespace AccessQueueService.Tests
    {
        public class AccessServiceTests
        {
            const int EXP_SECONDS = 5;
            const int EXP_MILLIS = 1000 * EXP_SECONDS;
            const int ACT_SECONDS = 1;
            const int ACT_MILLIS = 1000 * ACT_SECONDS;
            const int CAP_LIMIT = 5;
            const int BULK_COUNT = 10000;
            private readonly AccessService _accessService;

            public AccessServiceTests()
            {
                var inMemorySettings = new Dictionary<string, string?>
                {
                    { "AccessQueue:ExpirationSeconds", $"{EXP_SECONDS}" },
                    { "AccessQueue:ActivitySeconds", $"{ACT_SECONDS}" },
                    { "AccessQueue:CapacityLimit", $"{CAP_LIMIT}" },
                    { "AccessQueue:RollingExpiration", "true" }
                };

                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(inMemorySettings) // Add in-memory settings
                    .Build();

                _accessService = new AccessService(configuration);
            }

            [Fact]
            public async Task RequestAccess_ShouldGrantAccess_WhenCapacityIsAvailable()
            {
                // Arrange
                var userId = Guid.NewGuid();

                // Act
                var response = await _accessService.RequestAccess(userId);

                // Assert
                Assert.NotNull(response);
                Assert.NotNull(response.ExpiresOn);
                Assert.True(response.RequestsAhead == 0);
                Assert.Equal(1, _accessService.UnexpiredTicketsCount);
                Assert.Equal(1, _accessService.ActiveTicketsCount);
                Assert.Equal(0, _accessService.QueueCount);
            }

            [Fact]
            public async Task RequestAccess_ShouldReturnAccessResponse_WhenUserAlreadyHasTicket()
            {
                // Arrange
                var userId = Guid.NewGuid();
                await _accessService.RequestAccess(userId); // First request to create a ticket

                // Act
                var response = await _accessService.RequestAccess(userId); // Second request for the same user

                // Assert
                Assert.NotNull(response);
                Assert.NotNull(response.ExpiresOn);
                Assert.True(response.RequestsAhead == 0);
                Assert.Equal(1, _accessService.UnexpiredTicketsCount);
                Assert.Equal(1, _accessService.ActiveTicketsCount);
                Assert.Equal(0, _accessService.QueueCount);
            }

            [Fact]
            public async Task RequestAccess_ShouldQueueUser_WhenCapacityIsFull()
            {
                // Arrange
                for (int i = 0; i < CAP_LIMIT * 2; i++) // Fill double capacity
                {
                    await _accessService.RequestAccess(Guid.NewGuid());
                }
                var userId = Guid.NewGuid();

                // Act
                var response = await _accessService.RequestAccess(userId);

                // Assert
                Assert.NotNull(response);
                Assert.Null(response.ExpiresOn);
                Assert.True(response.RequestsAhead == 5);
                Assert.Equal(5, _accessService.UnexpiredTicketsCount);
                Assert.Equal(5, _accessService.ActiveTicketsCount);
                Assert.Equal(6, _accessService.QueueCount);
            }


            [Fact]
            public async Task RevokeAccess_ShouldReturnTrue_WhenUserHasAccess()
            {
                // Arrange
                var userId = Guid.NewGuid();
                await _accessService.RequestAccess(userId);

                // Act
                var result = await _accessService.RevokeAccess(userId);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public async Task RevokeAccess_ShouldReturnFalse_WhenUserDoesNotHaveAccess()
            {
                // Arrange
                var userId = Guid.NewGuid();

                // Act
                var result = await _accessService.RevokeAccess(userId);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task RequestAccess_ShouldQueueUser_AfterAccessRevoked()
            {
                var userId = Guid.NewGuid();
                await _accessService.RequestAccess(userId);

                for (int i = 0; i < CAP_LIMIT; i++) // Fill remaining slots
                {
                    await _accessService.RequestAccess(Guid.NewGuid());
                }

                var response = await _accessService.RequestAccess(userId); // Request access before revoking
                Assert.NotNull(response);
                Assert.True(response.HasAccess);

                await _accessService.RevokeAccess(userId); // Revoke access
                var responseAfterRevoke = await _accessService.RequestAccess(userId); // Request access again
                Assert.NotNull(responseAfterRevoke);
                Assert.False(responseAfterRevoke.HasAccess);
            }

            [Fact]
            public async Task RequestAccess_ShouldNotQueueUser_WhenMultipleRequestsForOtherUsersMade()
            {
                for (int i = 0; i < CAP_LIMIT; i++) // Fill slots without awaiting
                {
                    _ = _accessService.RequestAccess(Guid.NewGuid());
                }
                var response = await _accessService.RequestAccess(Guid.NewGuid()); // Request access before revoking
                Assert.NotNull(response);
                Assert.False(response.HasAccess);
            }

            [Fact]
            public async Task RequestAccess_ShouldUpdateExpirationTime_WhenRollingExpirationTrue()
            {
                var userId = Guid.NewGuid();
                var initialResponse = await _accessService.RequestAccess(userId);
                await Task.Delay(ACT_MILLIS);
                var updatedResponse = await _accessService.RequestAccess(userId);
                Assert.True(updatedResponse.ExpiresOn > initialResponse.ExpiresOn);
            }

            [Fact]
            public async Task RequestAccess_ShouldGrantAccess_WhenUsersWithAccessInactive()
            {
                for (int i = 0; i < CAP_LIMIT; i++)
                {
                    await _accessService.RequestAccess(Guid.NewGuid());
                }
                var userId = Guid.NewGuid();
                var response = await _accessService.RequestAccess(userId);
                Assert.False(response.HasAccess);
                await Task.Delay(ACT_MILLIS);
                response = await _accessService.RequestAccess(userId);
                Assert.True(response.HasAccess);
            }

            [Fact]
            public async Task RequestAccess_ShouldRevokeAccess_WhenExpired()
            {
                var userId = Guid.NewGuid();
                var response = await _accessService.RequestAccess(userId);
                Assert.True(response.HasAccess);
                await Task.Delay(EXP_MILLIS);
                for (int i = 0; i < CAP_LIMIT; i++)
                {
                    await _accessService.RequestAccess(Guid.NewGuid());
                }
                response = await _accessService.RequestAccess(userId);
                Assert.False(response.HasAccess);
            }

            [Fact]
            public async Task RequestAccess_ShouldRetailAccess_WhenNotExpired()
            {
                var userId = Guid.NewGuid();
                var response = await _accessService.RequestAccess(userId);
                Assert.True(response.HasAccess);
                await Task.Delay(ACT_MILLIS);
                for (int i = 0; i < CAP_LIMIT; i++)
                {
                    response = await _accessService.RequestAccess(Guid.NewGuid());
                    Assert.True(response.HasAccess);
                }
                response = await _accessService.RequestAccess(userId);
                Assert.True(response.HasAccess);
            }

            [Fact]
            public async Task RequestAccess_ShouldProcessBulkRequests()
            {
                var userId = Guid.NewGuid();
                await _accessService.RequestAccess(userId);
                for (int i = 0; i < BULK_COUNT; i++)
                {
                    _ = _accessService.RequestAccess(Guid.NewGuid());
                }
                var response = await _accessService.RequestAccess(userId);
                Assert.NotNull(response);
                Assert.True(response.HasAccess);
            }

            [Fact]
            public async Task RequestAccess_ShouldReportLessInQueue_AsTicketsInactivate()
            {
                var start = DateTime.UtcNow;
                for (int i = 0; i < CAP_LIMIT; i++) // Fill all slots
                {
                    var elapsed = DateTime.UtcNow - start;
                    Console.WriteLine($"Elapsed time: {elapsed.TotalSeconds} s: Adding {i}");
                    await _accessService.RequestAccess(Guid.NewGuid());
                    await Task.Delay(ACT_MILLIS / CAP_LIMIT);
                }
                var users = new[]
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid()
                };

                await _accessService.RequestAccess(users[0]);
                await _accessService.RequestAccess(users[1]);
                var response = await _accessService.RequestAccess(users[2]);
    
                Assert.Equal(1, response.RequestsAhead);
                await Task.Delay(ACT_MILLIS / CAP_LIMIT);

                await _accessService.RequestAccess(users[0]);
                await _accessService.RequestAccess(users[1]);
                response = await _accessService.RequestAccess(users[2]);

                Assert.Equal(0, response.RequestsAhead);
            }

        }
    }
}