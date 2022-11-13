using GraphQL.Types;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nekoyume.Action.ItemEnhancement;

namespace NineChronicles.Headless.GraphTypes.Abstractions
{
    internal class CombinationEventType: ObjectGraphType<ResultModel>
    {
        public class EnhancementResultType: EnumerationGraphType<EnhancementResult>
        {

        }
        public CombinationEventType()
        {
            Field<ListGraphType<GuidGraphType>>(
                nameof(ResultModel.materialItemIdList),
                resolve: context => context.Source.materialItemIdList.ToList()
                );
            Field<BigIntGraphType>(
                nameof(ResultModel.gold),
                resolve: context => context.Source.gold
                );
            Field<IntGraphType>(
                nameof(ResultModel.actionPoint),
                resolve: context => context.Source.actionPoint
                );
            Field<EnhancementResultType>(
                nameof(ResultModel.enhancementResult),
                resolve: context => context.Source.enhancementResult
                );
            Field<ItemUsableType>(
               nameof(ResultModel.itemUsable),
               resolve: context => context.Source.itemUsable
               );
            Field<ItemUsableType>(
                nameof(ResultModel.preItemUsable),
                resolve: context => context.Source.preItemUsable
                );
            Field<FungibleAssetValueType>(
                nameof(ResultModel.CRYSTAL),
                resolve: context => context.Source.CRYSTAL
                );

        }
    }
}
