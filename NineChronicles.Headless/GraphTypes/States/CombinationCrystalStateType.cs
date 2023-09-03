using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationCrystalStateType : ObjectGraphType<CombinationCrystalState>
    {
        public CombinationCrystalStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationCrystalState.CrystalCost),
                description: "Address of combination slot.",
                resolve: context => context.Source.CrystalCost);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationCrystalState.RecipeId),
                description: "Address of combination slot.",
                resolve: context => context.Source.RecipeId);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationCrystalState.MaxPoint),
                description: "Address of combination slot.",
                resolve: context => context.Source.MaxPoint);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationCrystalState.CurrentPoint),
                description: "Address of combination slot.",
                resolve: context => context.Source.CurrentPoint);
        }
    }
}
