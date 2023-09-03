using Libplanet;
using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationSimulationState
    {
        public long? blockIndex { get; set; }
        public List<CombinationSimulationResult>? result { get; set; }
        public decimal oneStarPercentage { get; set; }
        public decimal twoStarPercentage { get; set; }
        public decimal threeStarPercentage { get; set; }
        public decimal fourStarPercentage { get; set; }
        public decimal spellPercentage { get; set; }
    }
}
