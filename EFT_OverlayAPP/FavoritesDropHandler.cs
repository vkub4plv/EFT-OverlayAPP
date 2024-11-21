using GongSolutions.Wpf.DragDrop;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace EFT_OverlayAPP
{
    public class FavoritesDropHandler : IDropTarget
    {
        private readonly CraftingWindow craftingWindow;

        public FavoritesDropHandler(CraftingWindow window)
        {
            craftingWindow = window;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (!craftingWindow.IsFavoritesEditMode)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            if (dropInfo.Data is CraftableItem && (dropInfo.TargetItem is CraftableItem || dropInfo.TargetItem is CollectionViewGroup))
            {
                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (!craftingWindow.IsFavoritesEditMode)
            {
                return;
            }

            if (dropInfo.Data is CraftableItem sourceItem)
            {
                var favorites = craftingWindow.FavoriteItems;

                // Remove the item from the collection
                int oldIndex = favorites.IndexOf(sourceItem);
                if (oldIndex >= 0)
                {
                    favorites.RemoveAt(oldIndex);
                }

                // Calculate the correct insert index
                int insertIndex = GetAdjustedInsertIndex(dropInfo, favorites);

                // Insert the item
                if (insertIndex >= 0 && insertIndex <= favorites.Count)
                {
                    favorites.Insert(insertIndex, sourceItem);
                }
                else
                {
                    favorites.Add(sourceItem);
                }

                // Save the new order
                DataCache.SaveFavoriteItemOrder(favorites);
            }
        }

        private int GetAdjustedInsertIndex(IDropInfo dropInfo, IList<CraftableItem> favorites)
        {
            var targetItem = dropInfo.TargetItem;
            var targetGroup = dropInfo.TargetGroup;
            int insertIndex = 0;

            // Flatten the collection to get the correct index
            var flatList = GetFlatListFromCollectionView(craftingWindow.FavoritesView);

            if (targetItem is CraftableItem)
            {
                insertIndex = flatList.IndexOf(targetItem);

                if (dropInfo.InsertPosition == RelativeInsertPosition.AfterTargetItem)
                {
                    insertIndex++;
                }
            }
            else if (targetGroup != null)
            {
                // If dropping onto a group header, insert at the end of the group
                var groupItems = targetGroup.Items.Cast<CraftableItem>().ToList();
                if (groupItems.Any())
                {
                    var lastItem = groupItems.Last();
                    insertIndex = flatList.IndexOf(lastItem) + 1;
                }
                else
                {
                    // Empty group, find the index after the group header
                    insertIndex = flatList.IndexOf(targetGroup) + 1;
                }
            }
            else
            {
                // Insert at the end
                insertIndex = favorites.Count;
            }

            // Map the flat list index to the favorites collection index
            var targetItemInFavorites = flatList.ElementAtOrDefault(insertIndex) as CraftableItem;
            if (targetItemInFavorites != null)
            {
                insertIndex = favorites.IndexOf(targetItemInFavorites);
            }
            else
            {
                insertIndex = favorites.Count;
            }

            return insertIndex;
        }

        private List<object> GetFlatListFromCollectionView(ICollectionView collectionView)
        {
            var flatList = new List<object>();

            foreach (var item in collectionView)
            {
                if (item is CollectionViewGroup group)
                {
                    flatList.Add(group);
                    foreach (var groupItem in group.Items)
                    {
                        flatList.Add(groupItem);
                    }
                }
                else
                {
                    flatList.Add(item);
                }
            }

            return flatList;
        }
    }
}
