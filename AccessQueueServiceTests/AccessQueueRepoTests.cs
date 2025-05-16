using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AccessQueueService.Data;
using AccessQueueService.Models;
using AccessQueueService.Services;
using Microsoft.Extensions.Configuration;

namespace AccessQueueServiceTests
{
    public class AccessQueueRepoTests
    {
        private readonly TakeANumberAccessQueueRepo _repo;

        public AccessQueueRepoTests()
        {
            _repo = new TakeANumberAccessQueueRepo();
        }

        [Fact]
        public void GetUnexpiredTicketsCount_ReturnsCorrectCount()
        {

            _repo.UpsertTicket(new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow });
            _repo.UpsertTicket(new AccessTicket { UserId = "b", ExpiresOn = DateTime.UtcNow.AddMinutes(-1), LastActive = DateTime.UtcNow });
            Assert.Equal(1, _repo.GetUnexpiredTicketsCount());
        }

        [Fact]
        public void GetActiveTicketsCount_ReturnsCorrectCount()
        {

            var activeCutoff = DateTime.UtcNow.AddMinutes(-5);
            _repo.UpsertTicket(new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow });
            _repo.UpsertTicket(new AccessTicket { UserId = "b", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow.AddMinutes(-10) });
            Assert.Equal(1, _repo.GetActiveTicketsCount(activeCutoff));
        }

        [Fact]
        public void GetQueueCount_ReturnsCorrectCount()
        {

            Assert.Equal(0, _repo.GetQueueCount());
            _repo.Enqueue(new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow });
            Assert.Equal(1, _repo.GetQueueCount());
        }

        [Fact]
        public void GetRequestsAhead_ReturnsMinusOneIfUserNotInQueue()
        {

            Assert.Equal(-1, _repo.GetRequestsAhead("notfound"));
        }

        [Fact]
        public void GetRequestsAhead_ReturnsCorrectNumber()
        {

            var ticket = new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            _repo.Enqueue(ticket);
            Assert.Equal(0, _repo.GetRequestsAhead("a"));
        }

        [Fact]
        public void Enqueue_AddsTicketToQueue()
        {

            var ticket = new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            _repo.Enqueue(ticket);
            Assert.Equal(1, _repo.GetQueueCount());
            Assert.Equal(0, _repo.GetRequestsAhead("a"));
        }

        [Fact]
        public void DeleteExpiredTickets_RemovesExpiredTickets()
        {

            _repo.UpsertTicket(new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow.AddMinutes(-1), LastActive = DateTime.UtcNow });
            _repo.UpsertTicket(new AccessTicket { UserId = "b", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow });
            int removed = _repo.DeleteExpiredTickets();
            Assert.Equal(1, removed);
            Assert.NotNull(_repo.GetTicket("b"));
            Assert.Null(_repo.GetTicket("a"));
        }

        [Fact]
        public void DidDequeueUntilFull_FillsOpenSpots()
        {

            var ticket = new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow };
            _repo.Enqueue(ticket);
            bool result = _repo.DidDequeueUntilFull(60, 60, 1);
            Assert.True(result);
            Assert.NotNull(_repo.GetTicket("a"));
        }

        [Fact]
        public void DidDequeueUntilFull_ReturnsTrueIfNoOpenSpots()
        {

            _repo.UpsertTicket(new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow });
            bool result = _repo.DidDequeueUntilFull(60, 60, 0);
            Assert.True(result);
        }

        [Fact]
        public void GetTicket_ReturnsTicketIfExists()
        {

            var ticket = new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            _repo.UpsertTicket(ticket);
            var found = _repo.GetTicket("a");
            Assert.NotNull(found);
            Assert.Equal("a", found.UserId);
        }

        [Fact]
        public void GetTicket_ReturnsNullIfNotExists()
        {

            Assert.Null(_repo.GetTicket("notfound"));
        }

        [Fact]
        public void UpsertTicket_AddsOrUpdatesTicket()
        {

            var ticket = new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            _repo.UpsertTicket(ticket);
            Assert.NotNull(_repo.GetTicket("a"));
            var updated = new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow };
            _repo.UpsertTicket(updated);
            Assert.Equal(updated.ExpiresOn, _repo.GetTicket("a")!.ExpiresOn);
        }

        [Fact]
        public void RemoveUser_RemovesFromAllCollections()
        {

            var ticket = new AccessTicket { UserId = "a", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            _repo.Enqueue(ticket);
            _repo.UpsertTicket(ticket);
            bool removed = _repo.RemoveUser("a");
            Assert.True(removed);
            Assert.Null(_repo.GetTicket("a"));
            Assert.Equal(-1, _repo.GetRequestsAhead("a"));
        }

        [Fact]
        public void DidDequeueUntilFull_SkipsInactiveUser()
        {
            var inactive = new AccessTicket { UserId = "inactive", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow.AddMinutes(-10) };
            var active = new AccessTicket { UserId = "active", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow };
            _repo.Enqueue(inactive);
            _repo.Enqueue(active);
            bool result = _repo.DidDequeueUntilFull(5 * 60, 60, 1);
            Assert.True(result);
            Assert.Null(_repo.GetTicket("inactive"));
            Assert.NotNull(_repo.GetTicket("active"));
        }

        [Fact]
        public void Enqueue_QueuesUsersInOrder()
        {
            var ticket1 = new AccessTicket { UserId = "first", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            var ticket2 = new AccessTicket { UserId = "second", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            var ticket3 = new AccessTicket { UserId = "third", ExpiresOn = DateTime.UtcNow, LastActive = DateTime.UtcNow };
            _repo.Enqueue(ticket1);
            _repo.Enqueue(ticket2);
            _repo.Enqueue(ticket3);
            Assert.Equal(0, _repo.GetRequestsAhead("first"));
            Assert.Equal(1, _repo.GetRequestsAhead("second"));
            Assert.Equal(2, _repo.GetRequestsAhead("third"));
        }

        [Fact]
        public void DidDequeueUntilFull_DequeuesUsersInOrder()
        {
            var ticket1 = new AccessTicket { UserId = "first", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow };
            var ticket2 = new AccessTicket { UserId = "second", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow };
            var ticket3 = new AccessTicket { UserId = "third", ExpiresOn = DateTime.UtcNow.AddMinutes(1), LastActive = DateTime.UtcNow };
            _repo.Enqueue(ticket1);
            _repo.Enqueue(ticket2);
            _repo.Enqueue(ticket3);

            bool result = _repo.DidDequeueUntilFull(60 * 60, 60, 1);

            Assert.True(result);
            Assert.NotNull(_repo.GetTicket("first"));
            Assert.Null(_repo.GetTicket("second"));
            Assert.Null(_repo.GetTicket("third"));
        }
    }
}
