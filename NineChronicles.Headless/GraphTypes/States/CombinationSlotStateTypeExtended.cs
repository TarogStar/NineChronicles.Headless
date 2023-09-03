using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationSlotStateTypeExtended : ObjectGraphType<CombinationSlotStateExtended>
    {
        public CombinationSlotStateTypeExtended()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSlotStateExtended.SlotIndex),
                description: "Index of combination Slot",
                resolve: context => context.Source.SlotIndex);
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(CombinationSlotStateExtended.ItemGUID),
                description: "GUID of item in combination slot",
                resolve: context => context.Source.ItemGUID);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(CombinationSlotStateExtended.UnlockBlockIndex),
                description: "Block index at the combination slot can be usable.",
                resolve: context => context.Source.UnlockBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSlotStateExtended.Stars),
                description: "How many options/stars the equipment contains",
                resolve: context => context.Source.Stars);
        }
    }
}
