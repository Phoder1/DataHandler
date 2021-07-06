using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DataSaving
{
    #region Data Handler
    public static class DataHandler
    {
        private static readonly object WriteReadLock = new object();
        public static string persistentPath = Application.persistentDataPath;
        #region AutoSave
        ///// <summary>
        ///// The autosave interval in seconds.
        ///// </summary>
        //public static int autoSaveInterval = 300;
        //private static bool _autoSaveEnabled = false;
        //const float autoSaveRetryDelay = 2;
        //private static int retryCount;
        //const int maxRetryTime = 10;
        //public static event Action<bool> AutoSaveCallback = null;

        /// <param name="interval">The auto save interval in seconds.</param>
        //        public static void StartAutoSave(int? interval = null)
        //        {
        //            AutoSaveEnabled = true;

        //            if (interval != null)
        //                autoSaveInterval = (int)interval;
        //        }
        //        public static void StopAutoSave() => AutoSaveEnabled = false;
        //        #endregion
        //        #region internal
        //        private static async void AutoSaveAsync(float delay = 0)
        //        {
        //            if (AutoSaveEnabled == false)
        //                return;

        //            if (delay > 0)
        //            {
        //                await Task.Delay(TimeSpan.FromSeconds(autoSaveInterval));
        //                if (AutoSaveEnabled == false)
        //                    return;
        //            }

        //            var success = await SaveAllAsync();
        //            if (success)
        //            {
        //                retryCount = 0;
        //                AutoSaveCallback?.Invoke(success);
        //                AutoSaveAsync(autoSaveInterval);
        //            }
        //            else
        //            {
        //                if (retryCount == maxRetryTime)
        //                {
        //#if UNITY_EDITOR
        //                    Debug.LogError("Autosave max retry count reached!");
        //#endif
        //                    retryCount = 0;
        //                    AutoSaveCallback?.Invoke(success);
        //                    AutoSaveAsync(autoSaveInterval);
        //                    return;
        //                }
        //                retryCount++;
        //                AutoSaveAsync(autoSaveRetryDelay);
        //            }
        //        }
        /// <param name="interval">The auto save interval in seconds.</param>
        //        public static void StartAutoSave(int? interval = null)
        //        {
        //            AutoSaveEnabled = true;

        //            if (interval != null)
        //                autoSaveInterval = (int)interval;
        //        }
        //        public static void StopAutoSave() => AutoSaveEnabled = false;
        //        #endregion
        //        #region internal
        //        private static async void AutoSaveAsync(float delay = 0)
        //        {
        //            if (AutoSaveEnabled == false)
        //                return;

        //            if (delay > 0)
        //            {
        //                await Task.Delay(TimeSpan.FromSeconds(autoSaveInterval));
        //                if (AutoSaveEnabled == false)
        //                    return;
        //            }

        //            var success = await SaveAllAsync();
        //            if (success)
        //            {
        //                retryCount = 0;
        //                AutoSaveCallback?.Invoke(success);
        //                AutoSaveAsync(autoSaveInterval);
        //            }
        //            else
        //            {
        //                if (retryCount == maxRetryTime)
        //                {
        //#if UNITY_EDITOR
        //                    Debug.LogError("Autosave max retry count reached!");
        //#endif
        //                    retryCount = 0;
        //                    AutoSaveCallback?.Invoke(success);
        //                    AutoSaveAsync(autoSaveInterval);
        //                    return;
        //                }
        //                retryCount++;
        //                AutoSaveAsync(autoSaveRetryDelay);
        //            }
        //        }
        //public static bool AutoSaveEnabled
        //{
        //    get => _autoSaveEnabled;
        //    set
        //    {
        //        if (value == _autoSaveEnabled)
        //            return;

        //        _autoSaveEnabled = value;

        //        if (_autoSaveEnabled)
        //            AutoSaveAsync(0);
        //    }
        //}
        #endregion


        //The cache, any data that was loaded or created get cached
        private static readonly Dictionary<Type, ISaveable> dataDictionary = new Dictionary<Type, ISaveable>();
        #region Naming Conventions
        //Naming conventions are used to consistently determin the directory in which you save the data by it's type
        public static string DirectoryPath => persistentPath + "/Saves/";
        public static string GetFilePath(Type type) => DirectoryPath + GetFileName(type) + ".txt";
        public static string GetFileName(Type type) => type.ToString().Replace("+", "_");
        public static string GetJson(object saveObj) => JsonUtility.ToJson(saveObj, true);
        public static string[] GetJsons(object[] saveObj) => Array.ConvertAll(saveObj, (x) => JsonUtility.ToJson(x, true));
        public static bool DirectoryExists(string path) => Directory.Exists(path);
        public static bool FileExists(Type type) => File.Exists(GetFilePath(type));
        #endregion
        #region interface
        public static void OverrideData<T>(T value) where T : class, ISaveable, new()
        {

            if (dataDictionary.ContainsKey(typeof(T)))
            {
                if (dataDictionary[typeof(T)] != value)
                {
                    value.ValueChanged();
                    dataDictionary[typeof(T)] = value;
                }
            }
            else
            {
                value.ValueChanged();
                dataDictionary.Add(typeof(T), value);
            }
        }
        public static void ClearAllSavedData()
        {
            lock (WriteReadLock)
            {
                try
                {
                    var files = Directory.GetFiles(DirectoryPath);
                    foreach (var file in files)
                        File.Delete(file);
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
            }
        }
        #region Save
        public static void SaveAll(Action<bool> callback = null)
            => _ = SaveAllAsync(GetJsons(dataDictionary.Values.ToArray()), callback);
        public static void Save<T>(this T refrence, Action<bool> callback = null) where T : class, ISaveable, new() => refrence.GetType().Save(callback);
        public static void Save<T>(Action<bool> callback = null) where T : class, ISaveable, new() => typeof(T).Save(callback);
        public static void Save(this Type type, Action<bool> callback = null) => _ = SaveAsync(type, GetJson(dataDictionary[type]), callback);
        #endregion
        #region Cache
        public static void Cache<T>() where T : class, ISaveable, new() => Load<T>();
        public static void Cache<T>(this T refrence) where T : class, ISaveable, new() => Load<T>();
        #endregion
        #region Load
        public static void Load<T>(out T data) where T : class, ISaveable, new() => data = Load<T>();
        public static T Load<T>() where T : class, ISaveable, new()
        {
            if (dataDictionary.TryGetValue(typeof(T), out ISaveable instance))
                return (T)instance;

            if (!TryLoad(out T item))
                item = new T();

            dataDictionary.Add(typeof(T), item);

            return item;
        }
        #endregion
        #endregion
        #region Internal
        private static async Task<bool> SaveAllAsync(string[] data, Action<bool> callback = null)
        {
            bool success = true;

            var keys = dataDictionary.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
                success &= await SaveAsync(keys[i], data[i]);

            callback?.Invoke(success);
            return success;
        }
        private static async Task<bool> SaveAsync(Type type, string data, Action<bool> callback = null)
        {
            var task = new Task<bool>(() => TrySave(type, data));
            task.Start();
            var success = await task;

            callback?.Invoke(success);
            return success;
        }
        private static bool TrySave(Type type, string data)
        {
            if (!type.IsSerializable)
                throw new InvalidOperationException("A serializable Type is required");

            if (!dataDictionary.TryGetValue(type, out ISaveable objectToSave))
                return false;

            if (!objectToSave.IsDirty)
                return true;

            var filePath = GetFilePath(type);
            if (Directory.Exists(DirectoryPath))
                return Save();
            else
                if (CreateDirectory())
                return Save();
            return false;

            bool Save()
            {
                lock (WriteReadLock)
                {
                    try
                    {
                        File.WriteAllText(filePath, data);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                        return false;
                    }
                    objectToSave.Clean();
                    Debug.Log("Saved");
                    return true;
                }
            }
            bool CreateDirectory()
            {
                lock (WriteReadLock)
                {
                    try
                    {
                        Directory.CreateDirectory(DirectoryPath);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                        return false;
                    }
                    return Directory.Exists(DirectoryPath);
                }
            }
        }
        private static bool TryLoad(out object objectToLoad, Type type) => TryLoad(out objectToLoad, type);
        private static bool TryLoad<T>(out T objectToLoad) where T : class, ISaveable, new() => TryLoad(out objectToLoad, typeof(T));
        private static bool TryLoad<T>(out T objectToLoad, Type type)
        {
            objectToLoad = default;
            string filePath = GetFilePath(type);

            if (!FileExists(type))
                return false;

            string json = "";
            lock (WriteReadLock)
                try
                {
                    json = File.ReadAllText(filePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                }

            if (json == "")
                return false;

            if (JsonParser.TryParseJson(json, out objectToLoad))
                return true;

            if (objectToLoad == null)
                return false;

            return true;
        }
        #endregion
    }
    #endregion
    #region Dirty Data Interface
    /// <summary>
    /// A data interface which implements the IsDirt flag design pattern.
    /// <a href="https://gpgroup13.wordpress.com/a-dirty-flag-tutorial/#consider">(Refrence)</a>
    /// Make sure to run the ValueChanged function on every property change.
    /// </summary>
    public interface IDirtyData
    {
        bool IsDirty { get; }
        void Clean();
        void ValueChanged();
        event Action OnDirty;
        event Action OnValueChange;
    }
    public interface ISaveable : IDirtyData 
    {
    }
    public abstract class DirtyData : IDirtyData
    {
        private bool _isDirty = false;

        protected DirtyData(bool isDirty = true)
        {
            IsDirty = isDirty;
        }

        public virtual bool IsDirty
        {
            get => _isDirty;
            protected set
            {
                if (IsDirty == value)
                    return;

                _isDirty = value;

                if (IsDirty)
                {
                    OnDirty?.Invoke();
                }
            }
        }
        public event Action OnDirty;
        public event Action OnValueChange;

        public void Clean()
        {
            IsDirty = false;
            OnClean();
        }

        protected virtual void OnClean() { }

        public virtual void ValueChanged()
        {
            OnValueChange?.Invoke();
            IsDirty = true;
        }
        protected void Setter<T>(ref T data, T value, Action<T> onValueChangedAction = null)
        {
            if (IsDirty && onValueChangedAction == null && OnValueChange == null)
            {
                data = value;
                return;
            }

            if ((data == null && value == null) || (data != null && data.Equals(value)))
                return;

            data = value;

            onValueChangedAction?.Invoke(data);
            ValueChanged();
        }
    }
    #region Lists
    [Serializable]
    public abstract class BaseDirtyList<T> : DirtyData, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
    {
        #region List
        public List<T> collection = new List<T>();
        protected BaseDirtyList(bool isDirty = true) : base(isDirty) { }

        public T this[int index]
        {
            get => collection[index];
            set
            {
                if ((collection[index] == null && value == null) || (collection[index] != null && collection[index].Equals(value)))
                    return;

                collection[index] = value;

                ValueChanged();
            }
        }
        public int Count => collection.Count;
        public bool IsReadOnly => false;
        public void Add(T item)
        {
            collection.Add(item);
            ValueChanged();
        }
        public void Clear()
        {
            if (collection.Count != 0)
            {
                collection.Clear();

                ValueChanged();
            }
        }
        public bool Contains(T item) => collection.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => collection.CopyTo(array, arrayIndex);

        public int IndexOf(T item) => collection.IndexOf(item);
        public T Find(Predicate<T> match) => collection.Find(match);
        public T FindLast(Predicate<T> match) => collection.FindLast(match);
        public int FindLastIndex(int startIndex, int count, Predicate<T> match) => collection.FindLastIndex(startIndex, count, match);
        public int FindLastIndex(int startIndex, Predicate<T> match) => collection.FindLastIndex(startIndex, match);
        public int FindLastIndex(Predicate<T> match) => collection.FindLastIndex(match);

        public void Insert(int index, T item)
        {
            collection.Insert(index, item);
            ValueChanged();
        }

        public bool Remove(T item)
        {
            if (collection.Remove(item))
            {
                ValueChanged();
                return true;
            }
            return false;
        }

        public void RemoveAt(int index)
        {
            collection.RemoveAt(index);
            ValueChanged();
        }
        public int FindIndex(int startIndex, int count, Predicate<T> match) => collection.FindIndex(startIndex, count, match);
        public int FindIndex(int startIndex, Predicate<T> match) => collection.FindIndex(startIndex, match);
        public int FindIndex(Predicate<T> match) => collection.FindIndex(match);
        public void Sort(Comparison<T> comparison)
        {
            collection.Sort(comparison);

            ValueChanged();
        }
        public void Sort(int index, int count, IComparer<T> comparer)
        {
            collection.Sort(index, count, comparer);

            ValueChanged();
        }
        public void Sort()
        {
            collection.Sort();

            ValueChanged();
        }
        public void Sort(IComparer<T> comparer)
        {
            collection.Sort(comparer);

            ValueChanged();
        }
        public IEnumerator<T> GetEnumerator() => collection.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion
    }
    [Serializable]
    public class DirtyDataList<T> : BaseDirtyList<T> where T : IDirtyData
    {
        public DirtyDataList(bool isDirty = true) : base(isDirty) 
        {
            collection.ForEach((x) => x.OnValueChange += ValueChanged);
        }

        public override bool IsDirty
        {
            get => base.IsDirty || collection.Exists((X) => X.IsDirty);
            protected set => base.IsDirty = value;
        }
        protected override void OnClean()
        {
            base.OnClean();
            collection.ForEach((X) => X.Clean());
        }

    }
    [Serializable]
    public class DirtyStructList<T> : BaseDirtyList<T> where T : struct
    {
        public DirtyStructList(bool isDirty = true) : base(isDirty) { }
    }
    #endregion
    #endregion
    #region Json Parser
    public static class JsonParser
    {
        public static bool TryParseJson<T>(string json, out T jsonObject)
        {
            jsonObject = default;
            if (json.Length < 1 || json == "")
                return false;
            try
            {
                jsonObject = JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
            if (jsonObject == null)
                return false;
            return true;
        }
    }
    #endregion  
}