using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TexturePacker.Net
{
    public class ItemGroup : Item
    {
        private static readonly string[] folderImages = new string[] { "folder16.png", "folder32.png", "folder64.png", "folder128.png", "folder256.png" };

        static string GetFolderThumbnail()
        {
            int scale = Math.Min(folderImages.Length, (int)Math.Round(MainWindow.Instance.ScaleX));
            return Path.Combine(Environment.CurrentDirectory, "res", folderImages[Math.Max(scale, 1) - 1]);
        }

        public ItemGroup(string rootFolder, string currentFolder)
            : base(GetFolderThumbnail(), true)
        {
            Items = new ObservableCollection<Item>();
            InitializeItemGroup(rootFolder, currentFolder);
        }

        private void InitializeItemGroup(string rootFolder, string currentFolder)
        {
            if (rootFolder == currentFolder)
            {
                string directorySeparotor = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\\" : "/";
                folderName = rootFolder.Substring(rootFolder.LastIndexOf(directorySeparotor) + 1);
            }
            else
            {
                folderName = currentFolder.Substring(rootFolder.Length + 1);
            }
            string[] files = Directory.GetFiles(currentFolder, "*.png")
                .Concat(Directory.GetFiles(currentFolder, "*.jpg"))
                .ToArray();
            string[] directories = Directory.GetDirectories(currentFolder);
            if (files.Length > 0)
            {
                foreach (var fileName in files)
                {
                    Items.Add(new Item(fileName));
                }
                foreach (var dir in directories)
                {
                    var itemGroup = new ItemGroup(currentFolder, dir);
                    if (itemGroup.ContainsItems())
                    {
                        Items.Add(itemGroup);
                    }
                }
            }
            else
            {
                foreach (var dir in directories)
                {
                    var itemGroup = new ItemGroup(rootFolder, dir);
                    if (itemGroup.ContainsItems())
                    {
                        Items.Add(itemGroup);
                    }
                }
                if (Items.Count == 1)
                {
                    ItemGroup itemGroup = Items[0] as ItemGroup;
                    this.Items = itemGroup.Items;
                    this.folderName = itemGroup.folderName;
                }
                else if (Items.All(item => item.IsDirectory))
                {
                    foreach (ItemGroup itemGroup in Items)
                    {
                        itemGroup.folderName = itemGroup.folderName.Substring(this.folderName.Length + 1);
                    }
                }
            }
        }

        bool ContainsItems()
        {
            if (Items.Any(item => !(item is ItemGroup childGroup) || childGroup.ContainsItems()))
            {
                return true;
            }

            return false;
        }

        public IEnumerable<Item> GetAllItems(bool includeChildGroup)
        {
            var items = Items.Where(item => !item.IsDirectory);
            if (includeChildGroup)
            {
                foreach (ItemGroup itemGroup in Items.Where(item => item.IsDirectory))
                {
                    items = items.Concat(itemGroup.GetAllItems(true));
                }
            }
            return items;
        }

        public ObservableCollection<Item> Items { get; set; }

        private string folderName;
        public override string Name => folderName;
    }
}
