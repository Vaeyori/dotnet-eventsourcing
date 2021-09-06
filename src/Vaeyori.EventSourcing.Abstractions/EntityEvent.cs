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
    using Vaeyori.BlockChain.Abstractions;

    public abstract record EntityEvent : Block, IEntityEvent
    {
        public EntityEvent(
            Guid identity,
            DateTimeOffset when,
            string correlationId,
            string previousHash)
            : base(when, previousHash)
        {
            Identity = identity;
            When = when;
            CorrelationId = correlationId;

            Initialize();
        }

        public Guid Identity { get; init; }
        public string CorrelationId { get; internal set; }
        int IEntityEvent.Sequence { get; set; }

    }
}
