using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationSimulationStateType : ObjectGraphType<CombinationSimulationState>
    {
        public CombinationSimulationStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSimulationState.blockIndex),
                description: "Block Index",
                resolve: context => context.Source.blockIndex);
            Field<NonNullGraphType<ListGraphType<CombinationSimulationResultType>>>(
                nameof(CombinationSimulationState.result),
                description: "Block Index",
                resolve: context => context.Source.result);
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(CombinationSimulationState.oneStarPercentage),
                description: "Block Index",
                resolve: context => context.Source.oneStarPercentage);
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(CombinationSimulationState.twoStarPercentage),
                description: "Block Index",
                resolve: context => context.Source.twoStarPercentage);
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(CombinationSimulationState.threeStarPercentage),
                description: "Block Index",
                resolve: context => context.Source.threeStarPercentage);
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(CombinationSimulationState.fourStarPercentage),
                description: "Block Index",
                resolve: context => context.Source.fourStarPercentage);
            Field<NonNullGraphType<DecimalGraphType>>(
                nameof(CombinationSimulationState.spellPercentage),
                description: "Block Index",
                resolve: context => context.Source.spellPercentage);
        }
    }
}
