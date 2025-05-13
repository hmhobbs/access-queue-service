namespace AccessQueueServiceTests
{
    using global::AccessQueueService.Data;
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
            const int BULK_COUNT = 50000;
            private AccessService _accessService;
            public static IEnumerable<object[]> RepoImplementations()
            {
                yield return new object[] { new DictionaryAccessQueueRepo() };
                yield return new object[] { new TakeANumberAccessQueueRepo() };
            }

            private void CreateService(IAccessQueueRepo repo)
            {
                var inMemorySettings = new Dictionary<string, string?>
                {
                    { "AccessQueue:ExpirationSeconds", $"{EXP_SECONDS}" },
                    { "AccessQueue:ActivitySeconds", $"{ACT_SECONDS}" },
                    { "AccessQueue:CapacityLimit", $"{CAP_LIMIT}" },
                    { "AccessQueue:RollingExpiration", "true" }
                };

                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(inMemorySettings)
                    .Build();


                _accessService = new AccessService(configuration, repo);
            }

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldGrantAccess_WhenCapacityIsAvailable(IAccessQueueRepo repo)
            {
                CreateService(repo);
                var userId = Guid.NewGuid();

                var response = await _accessService.RequestAccess(userId);

                Assert.NotNull(response);
                Assert.NotNull(response.ExpiresOn);
                Assert.True(response.RequestsAhead == 0);
                Assert.Equal(1, _accessService.UnexpiredTicketsCount);
                Assert.Equal(1, _accessService.ActiveTicketsCount);
                Assert.Equal(0, _accessService.QueueCount);
            }

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldReturnAccessResponse_WhenUserAlreadyHasTicket(IAccessQueueRepo repo)
            {
                CreateService(repo);
                var userId = Guid.NewGuid();
                await _accessService.RequestAccess(userId);

                var response = await _accessService.RequestAccess(userId);

                Assert.NotNull(response);
                Assert.NotNull(response.ExpiresOn);
                Assert.True(response.RequestsAhead == 0);
                Assert.Equal(1, _accessService.UnexpiredTicketsCount);
                Assert.Equal(1, _accessService.ActiveTicketsCount);
                Assert.Equal(0, _accessService.QueueCount);
            }

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldQueueUser_WhenCapacityIsFull(IAccessQueueRepo repo)
            {
                CreateService(repo);
                for (int i = 0; i < CAP_LIMIT * 2; i++) // Fill double capacity
                {
                    await _accessService.RequestAccess(Guid.NewGuid());
                }
                var userId = Guid.NewGuid();

                var response = await _accessService.RequestAccess(userId);

                Assert.NotNull(response);
                Assert.Null(response.ExpiresOn);
                Assert.True(response.RequestsAhead == CAP_LIMIT);
                Assert.Equal(5, _accessService.UnexpiredTicketsCount);
                Assert.Equal(5, _accessService.ActiveTicketsCount);
                Assert.Equal(6, _accessService.QueueCount);
            }


            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RevokeAccess_ShouldReturnTrue_WhenUserHasAccess(IAccessQueueRepo repo)
            {
                CreateService(repo);
                var userId = Guid.NewGuid();
                await _accessService.RequestAccess(userId);

                var result = await _accessService.RevokeAccess(userId);

                Assert.True(result);
            }

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RevokeAccess_ShouldReturnFalse_WhenUserDoesNotHaveAccess(IAccessQueueRepo repo)
            {
                CreateService(repo);
                var userId = Guid.NewGuid();

                var result = await _accessService.RevokeAccess(userId);

                Assert.False(result);
            }

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldQueueUser_AfterAccessRevoked(IAccessQueueRepo repo)
            {
                CreateService(repo);
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

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldNotQueueUser_WhenMultipleRequestsForOtherUsersMade(IAccessQueueRepo repo)
            {
                CreateService(repo);
                for (int i = 0; i < CAP_LIMIT; i++) // Fill slots without awaiting
                {
                    _ = _accessService.RequestAccess(Guid.NewGuid());
                }
                var response = await _accessService.RequestAccess(Guid.NewGuid()); // Request access before revoking
                Assert.NotNull(response);
                Assert.False(response.HasAccess);
            }

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldUpdateExpirationTime_WhenRollingExpirationTrue(IAccessQueueRepo repo)
            {
                CreateService(repo);
                var userId = Guid.NewGuid();
                var initialResponse = await _accessService.RequestAccess(userId);
                await Task.Delay(ACT_MILLIS);
                var updatedResponse = await _accessService.RequestAccess(userId);
                Assert.True(updatedResponse.ExpiresOn > initialResponse.ExpiresOn);
            }

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldGrantAccess_WhenUsersWithAccessInactive(IAccessQueueRepo repo)
            {
                CreateService(repo);
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

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldRevokeAccess_WhenExpired(IAccessQueueRepo repo)
            {
                CreateService(repo);
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

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldRetailAccess_WhenNotExpired(IAccessQueueRepo repo)
            {
                CreateService(repo);
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

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldProcessBulkRequests(IAccessQueueRepo repo)
            {
                CreateService(repo);
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

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldReportLessInQueue_AsTicketsInactivate(IAccessQueueRepo repo)
            {
                CreateService(repo);
                var start = DateTime.UtcNow;
                for (int i = 0; i < CAP_LIMIT; i++)
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

            [Theory]
            [MemberData(nameof(RepoImplementations))]
            public async Task RequestAccess_ShouldShowCorrectRequestsAhead_WhenAccessRerequested(IAccessQueueRepo repo)
            {
                CreateService(repo);
                for (int i = 0; i < CAP_LIMIT; i++)
                {
                    await _accessService.RequestAccess(Guid.NewGuid());
                }

                var id1 = Guid.NewGuid();
                var id2 = Guid.NewGuid();
                var id3 = Guid.NewGuid();

                var response1 = await _accessService.RequestAccess(id1);
                var response2 = await _accessService.RequestAccess(id2);
                var response3 = await _accessService.RequestAccess(id3);

                Assert.Equal(0, response1.RequestsAhead);
                Assert.Equal(1, response2.RequestsAhead);
                Assert.Equal(2, response3.RequestsAhead);
                
                response1 = await _accessService.RequestAccess(id1);
                response2 = await _accessService.RequestAccess(id2);
                response3 = await _accessService.RequestAccess(id3);

                Assert.Equal(0, response1.RequestsAhead);
                Assert.Equal(1, response2.RequestsAhead);
                Assert.Equal(2, response3.RequestsAhead);
            }
        }
    }
}