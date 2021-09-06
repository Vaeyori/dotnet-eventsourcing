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

namespace Vaeyori.EventSourcing.Abstractions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class Entity
    {
        private IEntityEvent _previousEvent;

        private int _sequence = 0;
        private readonly Queue<IEntityEvent> _savedEvents = new();
        private readonly Queue<IEntityEvent> _unsavedEvents = new();

        protected Entity(EntityCreatedEvent createdEvent)
        {
            ReceiveEvent(createdEvent ?? throw new ArgumentNullException(nameof(createdEvent)));
        }

        protected Entity(IEnumerable<IEntityEvent> savedEvents)
        {
            if (savedEvents is null)
            {
                throw new ArgumentNullException(nameof(savedEvents));
            }

            if (!savedEvents.Any())
            {
                throw new ArgumentOutOfRangeException(nameof(savedEvents));
            }

            foreach(var group in savedEvents.GroupBy(x => x.CorrelationId))
            {
                foreach(IEntityEvent entityEvent in group.OrderBy(x => x.When).ThenBy(x => x.Sequence))
                {
                    IntegrateEvent(
                        entityEvent: entityEvent,
                        isNewEvent: false);
                }
            }
        }


        public Guid Identity { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset ModifiedAt { get; private set; }
        public bool IsModified => _unsavedEvents.Any();
        public IEnumerable<IEntityEvent> Events => _savedEvents.Concat(_unsavedEvents);
        public IEntityChangeset GetChangeset() => new Changeset(this, _unsavedEvents);
            

        protected string GetPreviousHash() => _previousEvent?.CalculateHash() ?? string.Empty;
        protected void ReceiveEvent(IEntityEvent entityEvent)
        {
            if (entityEvent is null)
            {
                throw new ArgumentNullException(nameof(entityEvent));
            }

            entityEvent.Sequence = ++_sequence;

            IntegrateEvent(
                entityEvent: entityEvent,
                isNewEvent: true);
        }

        private void IntegrateEvent(IEntityEvent entityEvent, bool isNewEvent = true)
        {
            if (entityEvent is not EntityCreatedEvent &&
                !entityEvent.Identity.Equals(Identity))
            {
                throw new EntityIdentityMismatchException(
                    message: "Identity associated with event does not match current entity.",
                    current: Identity,
                    @event: entityEvent.Identity);
            }

            if (entityEvent.When < ModifiedAt)
            {
                throw new InvalidOperationException("Entity has been modified since event took place.");
            }

            if (entityEvent is EntityCreatedEvent)
            {
                Identity = entityEvent.Identity;
                CreatedAt = entityEvent.When;
            }

            if (!IsValid(entityEvent))
            {
                throw new InvalidOperationException("Event data has been modified, unable to proceed.");
            }

            ModifiedAt = entityEvent.When;
            _previousEvent = entityEvent;

            ApplyEvent(entityEvent);

            if (isNewEvent)
            {
                _unsavedEvents.Enqueue(entityEvent);
            }
            else
            {
                _savedEvents.Enqueue(entityEvent);
            }
        }

        private void ApplyEvent(IEntityEvent aggregateEvent)
        {
            var method = GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(x => x.GetParameters().Any(x => x.ParameterType.Equals(aggregateEvent.GetType())));

            if (method is null)
            {
                throw new NotImplementedException($"HandleEvent method not implemented for EntityEvent of type '{aggregateEvent.GetType().Name}'.");
            }

            _ = method.Invoke(this, new[] { aggregateEvent });
        }

        private bool IsValid(IEntityEvent entityEvent)
        {
            if (entityEvent.Hash != entityEvent.CalculateHash())
            {
                return false;
            }

            if (entityEvent.PreviousHash != GetPreviousHash())
            {
                return false;
            }

            return true;
        }

        private ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var count = _unsavedEvents.Count();
            for(int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IEntityEvent entityEven = _unsavedEvents.Dequeue();
                _savedEvents.Enqueue(entityEven);
            }

            return ValueTask.CompletedTask;
        }

        private sealed record Changeset : IEntityChangeset
        {
            private readonly Entity _entity;
            public Changeset(
                Entity entity,
                IEnumerable<IEntityEvent> events)
            {
                _entity = entity;
                Events = events;
            }

            public IEnumerable<IEntityEvent> Events { get; }

            public ValueTask CommitAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                return _entity.CommitAsync(cancellationToken);
            }
        }
    }
}
