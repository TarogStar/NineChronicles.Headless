using Libplanet.Action;
using System;

namespace NineChronicles.Headless.GraphTypes
{
    public class LocalRandom : System.Random, IRandom
    {
        public int Seed
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public LocalRandom(int seed)
            : base(seed)
        {
        }
    }
}
