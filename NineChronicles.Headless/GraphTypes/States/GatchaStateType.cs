using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class GatchaStateType : ObjectGraphType<GatchaState>
    {
        public GatchaStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                    nameof(GatchaState.StageId),
                    resolve: context => context.Source.StageId);
            Field<NonNullGraphType<IntGraphType>>(
                    nameof(GatchaState.CurrentStarCount),
                    resolve: context => context.Source.CurrentStarCount);
            Field<NonNullGraphType<IntGraphType>>(
                    nameof(GatchaState.RequiredStarCount),
                    resolve: context => context.Source.RequiredStarCount);
        }
    }
}
