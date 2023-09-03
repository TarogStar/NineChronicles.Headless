using Libplanet;
using Libplanet.Crypto;
using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StageResultInfo
    {
        public Address AvatarAddress;
        public int Stage;
        public double Wave0 { get; set; }
        public double Wave1 { get; set; }
        public double Wave2 { get; set; }
        public double Wave3 { get; set; }
    }
}
