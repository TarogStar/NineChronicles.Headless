using Libplanet;
using Libplanet.Crypto;
using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class GatchaState
    {
        public int StageId {get; set; }
        public int CurrentStarCount { get; set; }
        public int RequiredStarCount { get; set; }
    }
}
