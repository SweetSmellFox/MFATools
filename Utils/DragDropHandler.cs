﻿using System.Collections;
using System.Collections.ObjectModel;
using GongSolutions.Wpf.DragDrop;
using MFATools.ViewModels;


namespace MFATools.Utils;

public class DragDropHandler : IDropTarget
{
    public void DragOver(IDropInfo dropInfo)
    {
        // 确保拖动有效性并显示可视化反馈
        if (dropInfo is { Data: not null, TargetCollection: not null })
        {
            dropInfo.Effects = System.Windows.DragDropEffects.Move;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
        }
    }

    public void Drop(IDropInfo dropInfo)
    {
        // 当拖放完成后
        if (dropInfo is { Data: not null, TargetCollection: IList targetCollection })
        {
            var insertIndex = dropInfo.InsertIndex;

            // 确保移除和插入顺序正确，处理新索引
            if (dropInfo.Data is IEnumerable items)
            {
                foreach (var item in items)
                {
                    var oldIndex = targetCollection.IndexOf(item);

                    if (oldIndex != -1)
                    {
                        // 如果拖拽在向下移动，则需要修正插入索引
                        if (insertIndex > oldIndex)
                        {
                            insertIndex--;
                        }

                        targetCollection.RemoveAt(oldIndex);
                        targetCollection.Insert(insertIndex, item);
                    }
                }
            }
            else
            {
                // 处理单个项目的拖拽
                var item = dropInfo.Data;
                var oldIndex = targetCollection.IndexOf(item);

                if (oldIndex != -1)
                {
                    if (insertIndex > oldIndex)
                    {
                        insertIndex--;
                    }

                    targetCollection.RemoveAt(oldIndex);
                    targetCollection.Insert(insertIndex, item);
                }
            }


            if (targetCollection is ObservableCollection<DragItemViewModel> dragItemViewModels)
            {
                List<TaskInterfaceItem> tasks = new();
                foreach (var VARIABLE in dragItemViewModels)
                {
                    if (VARIABLE.InterfaceItem != null)
                        tasks.Add(VARIABLE.InterfaceItem);
                }

                if (MaaInterface.Instance != null)
                    MaaInterface.Instance.Task = tasks;
                // 保存当前的 ItemsSource 到 JSON
                JsonHelper.WriteToJsonFilePath(AppDomain.CurrentDomain.BaseDirectory, "interface",
                    MaaInterface.Instance);
            }
        }
    }
}