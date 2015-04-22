﻿/* Copyright 2010-2014 MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver.Linq.Expressions
{
    internal class CorrelatedAccumulatorExpression : ExtensionExpression
    {
        private readonly Guid _correlationId;
        private readonly AccumulatorExpression _accumulator;

        public CorrelatedAccumulatorExpression(Guid correlationId, AccumulatorExpression accumulator)
        {
            _correlationId = correlationId;
            _accumulator = Ensure.IsNotNull(accumulator, "accumulator");
        }

        public AccumulatorExpression Accumulator
        {
            get { return _accumulator; }
        }

        public Guid CorrelationId
        {
            get { return _correlationId; }
        }

        public override ExtensionExpressionType ExtensionType
        {
            get { return ExtensionExpressionType.CorrelatedAccumulator; }
        }

        public override Type Type
        {
            get { return _accumulator.Type; }
        }

        public CorrelatedAccumulatorExpression Update(AccumulatorExpression accumulator)
        {
            if (accumulator != _accumulator)
            {
                return new CorrelatedAccumulatorExpression(_correlationId, accumulator);
            }

            return this;
        }

        protected internal override System.Linq.Expressions.Expression Accept(ExtensionExpressionVisitor visitor)
        {
            return visitor.VisitCorrelatedAccumulator(this);
        }
    }
}