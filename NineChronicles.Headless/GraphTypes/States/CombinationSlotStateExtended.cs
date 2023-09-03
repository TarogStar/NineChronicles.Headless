using Libplanet;
using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationSlotStateExtended
    {
        public int SlotIndex { get; set; }
        public int Stars { get; set; }
        public Guid ItemGUID { get; set; }
        public long UnlockBlockIndex { get; set; }
        public int Spell { get; set; }
    }
}
