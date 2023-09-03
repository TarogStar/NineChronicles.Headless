using Libplanet;
using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationCrystalState
    {
        public int CrystalCost { get; set; }
        public int RecipeId { get; set; }
        public int MaxPoint { get; set; }
        public int CurrentPoint { get; set; }
    }
}
