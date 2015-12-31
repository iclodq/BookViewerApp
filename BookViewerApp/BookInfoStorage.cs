﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

namespace BookViewerApp
{
    public class BookInfoStorage
    {
        private const string fileName = "Bookinfo.xml";
        private static Windows.Storage.StorageFolder DataFolderRoaming { get { return Windows.Storage.ApplicationData.Current.RoamingFolder; } }
        private static Windows.Storage.StorageFolder DataFolderLocal { get { return Windows.Storage.ApplicationData.Current.LocalFolder; } }

        static System.Threading.SemaphoreSlim fileRoamingSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        static System.Threading.SemaphoreSlim fileLocalSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        internal static async Task<Windows.Storage.StorageFile> GetDataFileRoamingAsync()
        {
            return (Windows.Storage.StorageFile)(await DataFolderRoaming.TryGetItemAsync(fileName));
        }

        internal static async Task<Windows.Storage.StorageFile> GetDataFileLocalAsync()
        {
            return (Windows.Storage.StorageFile)(await DataFolderLocal.TryGetItemAsync(fileName));
        }

        internal static async Task<BookInfo[]> LoadAsync()
        {
            var infoRoaming = (await LoadAsyncOne(await GetDataFileRoamingAsync(), fileRoamingSemaphore) ?? new BookInfo[0]).ToList();
            var infoLocal = (await LoadAsyncOne(await GetDataFileLocalAsync(), fileLocalSemaphore) ?? new BookInfo[0]).ToList();
            foreach(var item in infoLocal)
            {
                if (infoRoaming.FindIndex((s) => s.ID == item.ID) == -1)
                {
                    infoRoaming.Add(item);
                }
            }
            return infoRoaming.ToArray();
        }

        internal static async Task<BookInfo[]> LoadAsyncOne(Windows.Storage.StorageFile file,System.Threading.SemaphoreSlim sem)
        {
            if (file == null) return null;

            await sem.WaitAsync();
            try
            {
                using (var s = (await file.OpenAsync(Windows.Storage.FileAccessMode.Read)).AsStream())
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BookInfo[]));
                    return (BookInfo[])serializer.Deserialize(s);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                sem.Release();
            }
        }

        internal static async Task SaveAsync()
        {
            var bookinfo = await GetBookInfoAsync();
            bookinfo.Sort((a, b) => b.ReadTimeLast.CompareTo(a.ReadTimeLast));
            await SaveDataLocalAsync(bookinfo.GetRange(0,Math.Min(bookinfo.Count, MaxBookmarkSaveCountLocal)).ToArray());
            await SaveDataRoamingAsync(bookinfo.GetRange(0, Math.Min(bookinfo.Count, MaxBookmarkSaveCountRoaming)).ToArray());
        }

        /// <summary>
        /// Number of BookInfo saved in RoamingState floder.
        /// </summary>
        private const int MaxBookmarkSaveCountRoaming = 50;
        /// <summary>
        /// Nuber of BookInfo saved in LocalState folder.
        /// </summary>
        private const int MaxBookmarkSaveCountLocal = 10000;

        private static async Task SaveDataRoamingAsync(BookInfo[] items)
        {
            await fileRoamingSemaphore.WaitAsync();
            try
            {
                var f = await DataFolderRoaming.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                using (var s = (await f.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite)).AsStream())
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BookInfo[]));
                    serializer.Serialize(s, items);
                }
            }
            catch {  }
            finally
            {
                fileRoamingSemaphore.Release();
            }
        }

        private static async Task SaveDataLocalAsync(BookInfo[] items)
        {
            await fileLocalSemaphore.WaitAsync();
            try
            {
                var f = await DataFolderLocal.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                using (var s = (await f.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite)).AsStream())
                {
                    var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BookInfo[]));
                    serializer.Serialize(s, items);
                }
            }
            catch { }
            finally
            {
                fileLocalSemaphore.Release();
            }
        }

        private static List<BookInfo> BookInfosCache;

        public static async Task<List<BookInfo>> GetBookInfoAsync()
        {
            if (BookInfosCache == null) BookInfosCache = ((await LoadAsync()) ?? new BookInfo[0]).ToList();
            return BookInfosCache;
        }

        public async static Task<BookInfo> GetBookInfoByIDAsync(string id)
        {
            var bis = (await GetBookInfoAsync());
            foreach(var item in bis)
            {
                if (item.ID == id) { return item; }
            }

            var book = new BookInfo() { ID = id };
            bis.Add(book);
            return book;
        }

        public class BookInfo
        {
            public string ID = "";
            public List<BookmarkItem> Bookmarks = new List<BookmarkItem>();

            public DateTime ReadTimeFirst = DateTime.MinValue;
            private DateTime ReadTimeThis;
            public DateTime ReadTimeLast;
            public double ReadTimeSpan;

            public BookInfo()
            {
                ReadTimeLast = ReadTimeThis = DateTime.Now;
                if (ReadTimeFirst == DateTime.MinValue) ReadTimeFirst = DateTime.Now;
            }

            public BookmarkItem GetLastReadPage()
            {
                return Bookmarks.Find((s) => s.Type == BookmarkItem.BookmarkItemType.LastRead);
            }

            public void SetLastReadPage(uint page)
            {
                var lastread = GetLastReadPage();
                if (lastread != null) Bookmarks.Remove(lastread);
                Bookmarks.Add(new BookmarkItem() { Page = page, Type = BookmarkItem.BookmarkItemType.LastRead });

                ReadTimeLast = DateTime.Now;
                ReadTimeSpan += (DateTime.Now - ReadTimeThis).TotalMilliseconds;
                ReadTimeThis = DateTime.Now;
            }

            public class BookmarkItem
            {
                public uint Page = 0;
                public BookmarkItemType Type = BookmarkItemType.UserDefined;
                public enum BookmarkItemType
                {
                    LastRead, UserDefined
                }
            }
        }

    }
}