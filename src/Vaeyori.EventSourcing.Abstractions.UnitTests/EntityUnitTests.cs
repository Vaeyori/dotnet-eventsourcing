/*
*    Copyright (C) 2021 Joshua "ysovuka" Thompson
*
*    This program is free software: you can redistribute it and/or modify
*    it under the terms of the GNU Affero General Public License as published
*    by the Free Software Foundation, either version 3 of the License, or
*    (at your option) any later version.
*
*    This program is distributed in the hope that it will be useful,
*    but WITHOUT ANY WARRANTY; without even the implied warranty of
*    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*    GNU Affero General Public License for more details.
*
*    You should have received a copy of the GNU Affero General Public License
*    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Vaeyori.EventSourcing.Abstractions.UnitTests
{
    public sealed class EntityUnitTests
    {
        private class TestEntity : Entity
        {
            public TestEntity(
                Guid identity,
                DateTimeOffset when,
                string correlationId)
                :
                base(new InternalCreatedEvent(identity, when, correlationId, string.Empty))
            {
            }

            public TestEntity(EntityCreatedEvent createdEvent)
                : base(createdEvent)
            {

            }

            public TestEntity(IEnumerable<IEntityEvent> savedEvents)
                : base(savedEvents)
            {

            }

            private void HandleEvent(CreatedEvent creatdEvent)
            {

            }

            private void HandleEvent(InternalCreatedEvent creatdEvent)
            {

            }

            private void HandleEvent(EditableHashEvent creatdEvent)
            {

            }


            public void PassNullEvent()
            {
                ReceiveEvent(null);
            }

            public void PassInvalidIdentityToEvent()
            {
                var e = new Event(Guid.NewGuid(), DateTimeOffset.UtcNow, "Test", GetPreviousHash());

                ReceiveEvent(e);
            }

            public void PassInvalidEvent()
            {
                var e = new Event(Identity, ModifiedAt.AddYears(-1), "Test", GetPreviousHash());

                ReceiveEvent(e);
            }

            private sealed record InternalCreatedEvent
                : EntityCreatedEvent
            {
                public InternalCreatedEvent(
                    Guid identity,
                    DateTimeOffset when,
                    string correlationId,
                    string previousHash)
                :base(identity, when, correlationId, previousHash)
                {
                    Initialize();
                }
            };

            private sealed record Event
                : EntityEvent
            {
                public Event(
                    Guid identity,
                    DateTimeOffset when,
                    string correlationId,
                    string previousHash)
                : base(identity, when, correlationId, previousHash)
                {
                    Initialize();
                }
            };
        }

        private sealed record CreatedEvent
            : EntityCreatedEvent
        {
            public CreatedEvent(
                Guid identity,
                DateTimeOffset when,
                string correlationId,
                string previousHash)
            : base(identity, when, correlationId, previousHash)
            {
                Initialize();
            }
        };

        private sealed record MethodNullEvent
            : EntityEvent
        {
            public MethodNullEvent(
                Guid identity,
                DateTimeOffset when,
                string correlationId,
                string previousHash)
            : base(identity, when, correlationId, previousHash)
            {
                Initialize();
            }
        };

        private sealed record EditableHashEvent
            : EntityEvent
        {
            public EditableHashEvent(
                Guid identity,
                DateTimeOffset when,
                string correlationId,
                string previousHash)
            : base(identity, when, correlationId, previousHash)
            {
                Test = "TesT";
                Initialize();
            }

            public string Test { get; set; }

            public void Init() => Initialize();
        }

        [Fact]
        public void Entity_Constructor_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TestEntity(createdEvent: null));
            Assert.Throws<ArgumentNullException>(() => new TestEntity(savedEvents: null));
        }

        [Fact]
        public void Entity_Constructor_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TestEntity(savedEvents: new List<IEntityEvent>()));
        }


        [Fact]
        public void Entity_ConstructorSavedEvents_Successful()
        {
            var createdEvent = new CreatedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "Test", "");

            var entity = new TestEntity(new List<IEntityEvent> { createdEvent });

            Assert.NotNull(entity);
            Assert.Single(entity.Events);
        }

        [Fact]
        public void Entity_ConstructorSavedEvents_ThrowsNotImplementedException()
        {
            var identity = Guid.NewGuid();
            var createdEvent = new CreatedEvent(identity, DateTimeOffset.UtcNow, "Test", "");
            var methodNullEvent = new MethodNullEvent(identity, DateTimeOffset.UtcNow, "Test", createdEvent.Hash);

            Assert.Throws<NotImplementedException>(() => new TestEntity(new List<IEntityEvent> { createdEvent, methodNullEvent }));
        }

        [Fact]
        public void Entity_ConstructorSavedEvents_ThrowsInvalidOperationException()
        {
            var identity = Guid.NewGuid();
            var createdEvent = new CreatedEvent(identity, DateTimeOffset.UtcNow, "Test", "");
            var methodNullEvent = new MethodNullEvent(identity, DateTimeOffset.UtcNow, "Test", "");

            Assert.Throws<InvalidOperationException>(() => new TestEntity(new List<IEntityEvent> { createdEvent, methodNullEvent }));
        }

        [Fact]
        public void Entity_ConstructorSavedEvents_ThrowsInvalidOperationException2()
        {
            var identity = Guid.NewGuid();
            var createdEvent = new CreatedEvent(identity, DateTimeOffset.UtcNow, "Test", "");
            var editableHashEvent = new EditableHashEvent(identity, DateTimeOffset.UtcNow, "Test", createdEvent.Hash);
            editableHashEvent.Test = ";";

            Assert.NotEqual(editableHashEvent.CalculateHash(), editableHashEvent.Hash);
            Assert.Throws<InvalidOperationException>(() => new TestEntity(new List<IEntityEvent> { createdEvent, editableHashEvent }));
        }

        [Theory]
        [InlineData("2745B89F-538B-4E6A-9699-793A5E5E1975", "Test", "2021/08/06 00:00:00", 1)]
        public void Entity_Constructor_Successful(
            string identity,
            string correlationId,
            string timestamp,
            int expectedEvents)
        {
            var entityIdentity = new Guid(identity);
            var when = DateTimeOffset.Parse(timestamp);

            var Entity = new TestEntity(entityIdentity, when, correlationId);
            
            Assert.Equal(expectedEvents, Entity.Events.Count());
            Assert.Equal(entityIdentity, Entity.Identity);
            Assert.Equal(when, Entity.CreatedAt);
            Assert.Equal(when, Entity.ModifiedAt);
        }

        [Fact]
        public async Task Entity_GetChangeset_SuccessfullyCommitAsync()
        {
            var entity = new TestEntity(Guid.NewGuid(), DateTimeOffset.UtcNow, "Test");

            Assert.True(entity.IsModified);

            var changeset = entity.GetChangeset();

            Assert.NotNull(changeset);
            Assert.NotEmpty(changeset.Events);

            await changeset.CommitAsync();

            Assert.False(entity.IsModified);
        }

        [Fact]
        public void Entity_ReceiveEvent_ThrowsArgumentNullException()
        {
            var identity = new Guid("2745B89F-538B-4E6A-9699-793A5E5E1975");
            var correlationId = "Test";
            var when = DateTimeOffset.Parse("2021/08/06 00:00:00");

            var Entity = new TestEntity(identity, when, correlationId);

            Assert.Throws<ArgumentNullException>(() => Entity.PassNullEvent());
        }

        [Fact]
        public void Entity_ReceiveEvent_ThrowsEntityIdentityMismatchException()
        {
            var identity = new Guid("2745B89F-538B-4E6A-9699-793A5E5E1975");
            var correlationId = "Test";
            var when = DateTimeOffset.Parse("2021/08/06 00:00:00");

            var Entity = new TestEntity(identity, when, correlationId);

            Assert.Throws<EntityIdentityMismatchException>(() => Entity.PassInvalidIdentityToEvent());
        }

        [Fact]
        public void Entity_ReceiveEvent_ThrowsInvalidOperationException()
        {
            var identity = new Guid("2745B89F-538B-4E6A-9699-793A5E5E1975");
            var correlationId = "Test";
            var when = DateTimeOffset.Parse("2021/08/06 00:00:00");

            var Entity = new TestEntity(identity, when, correlationId);

            Assert.Throws<InvalidOperationException>(() => Entity.PassInvalidEvent());
        }
    }
}
