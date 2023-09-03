using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StageResultInfoStateType : ObjectGraphType<StageResultInfo>
    {
        public StageResultInfoStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                    nameof(StageResultInfo.AvatarAddress),
                    resolve: context => context.Source.AvatarAddress);
            Field<NonNullGraphType<IntGraphType>>(
                    nameof(StageResultInfo.Stage),
                    resolve: context => context.Source.Stage);
            Field<NonNullGraphType<DecimalGraphType>>(
                    nameof(StageResultInfo.Wave0),
                    resolve: context => context.Source.Wave0);
            Field<NonNullGraphType<DecimalGraphType>>(
                    nameof(StageResultInfo.Wave1),
                    resolve: context => context.Source.Wave1);
            Field<NonNullGraphType<DecimalGraphType>>(
                    nameof(StageResultInfo.Wave2),
                    resolve: context => context.Source.Wave2);
            Field<NonNullGraphType<DecimalGraphType>>(
                    nameof(StageResultInfo.Wave3),
                    resolve: context => context.Source.Wave3);
        }
    }
}
