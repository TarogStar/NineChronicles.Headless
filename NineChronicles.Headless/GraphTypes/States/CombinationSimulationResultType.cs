using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    internal class CombinationSimulationResultType : ObjectGraphType<CombinationSimulationResult>
    {
        public CombinationSimulationResultType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSimulationResult.seed),
                resolve: context => context.Source.seed);
            Field<NonNullGraphType<FloatGraphType>>(
                nameof(CombinationSimulationResult.spellChance),
                resolve: context => context.Source.spellChance);
            Field<NonNullGraphType<FloatGraphType>>(
                nameof(CombinationSimulationResult.spellPower),
                resolve: context => context.Source.spellPower);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSimulationResult.starCount),
                resolve: context => context.Source.starCount);
        }
    }
}
