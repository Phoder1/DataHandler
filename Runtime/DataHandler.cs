using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace DataSaving
{
    #region Data Handler
    public static class DataHandler
    {
        private static readonly object WriteReadLock = new object();
        public static string persistentPath = Application.persistentDataPath;
        private static bool _autoSaveEnabled = false;
        public static bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                if (value == _autoSaveEnabled)
                    return;

                _autoSaveEnabled = value;

                if (_autoSaveEnabled)
                    AutoSaveAsync(0);
            }
        }
        /// <summary>
        /// The autosave interval in seconds.
        /// </summary>
        public static int autoSaveInterval = 300;
        const float autoSaveRetryDelay = 2;
        private static int retryCount;
        const int maxRetryTime = 10;
        public static event Action<bool> AutoSaveCallback = null;

        //The cache, any data that was loaded or created get cached
        private static readonly Dictionary<Type, IDirtyData> dataDictionary = new Dictionary<Type, IDirtyData>();
        #region Naming Conventions
        //Naming conventions are used to consistently determin the directory in which you save the data by it's type
        private static string DirectoryPath => persistentPath + "/Saves/";
        private static string GetFilePath(Type type) => DirectoryPath + GetFileName(type) + ".txt";
        private static string GetFileName(Type type) => type.ToString().Replace("+", "_");
        private static string GetJson(object saveObj) => JsonUtility.ToJson(saveObj, true);
        private static bool FileExists(Type type) => File.Exists(GetFilePath(type));
        #endregion
        #region interface
        public static T GetData<T>() where T : class, IDirtyData, new()
        {
            if (dataDictionary.TryGetValue(typeof(T), out IDirtyData instance))
                return (T)instance;

            if (!TryLoad(out T item))
                item = new T();

            dataDictionary.Add(typeof(T), item);

            return item;
        }
        public static void SetData<T>(T value) where T : class, IDirtyData, new()
        {
            if (dataDictionary.ContainsKey(typeof(T)))
                dataDictionary[typeof(T)] = value;
            else
                dataDictionary.Add(typeof(T), value);
        }
        public static void SaveAll(Action<bool> callback = null) => _ = SaveAllAsync(callback);
        public static async Task<bool> SaveAllAsync(Action<bool> callback = null)
        {
            bool success = true;
            foreach (var key in dataDictionary.Keys)
                success &= await SaveAsync(key);
            callback?.Invoke(success);
            return success;
        }
        public static void Save(Type type, Action<bool> callback = null) => _ = SaveAsync(type, callback);
        public static async Task<bool> SaveAsync(Type type, Action<bool> callback = null)
        {
            var task = new Task<bool>(() => TrySave(type));
            task.Start();
            var success = await task;
            callback?.Invoke(success);
            return success;
        }
        /// <param name="interval">The auto save interval in seconds.</param>
        public static void StartAutoSave(int? interval = null)
        {
            AutoSaveEnabled = true;

            if (interval != null)
                autoSaveInterval = (int)interval;
        }
        public static void StopAutoSave() => AutoSaveEnabled = false;
        #endregion
        #region internal
        private static async void AutoSaveAsync(float delay = 0)
        {
            if (AutoSaveEnabled == false)
                return;

            if (delay > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(autoSaveInterval));
                if (AutoSaveEnabled == false)
                    return;
            }

            var success = await SaveAllAsync();
            if (success)
            {
                retryCount = 0;
                AutoSaveCallback?.Invoke(success);
                AutoSaveAsync(autoSaveInterval);
            }
            else
            {
                if (retryCount == maxRetryTime)
                {
#if UNITY_EDITOR
                    Debug.LogError("Autosave max retry count reached!");
#endif
                    retryCount = 0;
                    AutoSaveCallback?.Invoke(success);
                    AutoSaveAsync(autoSaveInterval);
                    return;
                }
                retryCount++;
                AutoSaveAsync(autoSaveRetryDelay);
            }
        }
        private static bool TrySave(Type type)
        {
            if (!type.IsSerializable)
                throw new InvalidOperationException("A serializable Type is required");

            if (!dataDictionary.TryGetValue(type, out IDirtyData objectToSave))
                return false;

            if (!objectToSave.IsDirty)
                return true;

            var data = (GetJson(objectToSave));
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
                    objectToSave.Saved();
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
        private static bool TryLoad<T>(out T objectToLoad) where T : class, IDirtyData, new()
        {
            objectToLoad = default;
            string filePath = GetFilePath(typeof(T));

            if (!FileExists(typeof(T)))
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
        void Saved();
        void ValueChanged();
        event Action OnDirty;
    }
    public abstract class DirtyData : IDirtyData
    {
        private bool _isDirty = false;
        public virtual bool IsDirty
        {
            get => _isDirty;
            protected set => Setter(ref _isDirty, value, () => OnDirty?.Invoke(), () => _isDirty);
        }

        public event Action OnDirty;

        public void Saved()
        {
            IsDirty = false;
            OnDirty?.Invoke();
            OnSaved();
        }

        protected virtual void OnSaved() { }

        public virtual void ValueChanged() => IsDirty = true;
        protected void Setter<T>(ref T data, T value, Action onValueChanged, Func<bool> changeCondition = null)
        {
            if ((data == null && value == null) || (data != null && data.Equals(value)))
                return;
            data = value;

            if (changeCondition == null || changeCondition())
                onValueChanged?.Invoke();
        }
        protected void Setter<T>(ref T data, T value) => Setter<T>(ref data, value, ValueChanged);
    }
    #region Lists
    [Serializable]
    public abstract class BaseDirtyList<T> : DirtyData, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
    {
        #region List
        [SerializeField]
        protected List<T> collection = new List<T>();
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
            if(collection.Count != 0)
            {
            collection.Clear();

                ValueChanged();
            }
        }
        public bool Contains(T item) => collection.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => collection.CopyTo(array, arrayIndex);

        public int IndexOf(T item) => collection.IndexOf(item);

        public void Insert(int index, T item)
        {
            collection.Insert(index, item);
            ValueChanged();
        }

        public bool Remove(T item)
        {
            if(collection.Remove(item))
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
        public int FindIndex(Predicate<T> match) => FindIndex(match);
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
        public override bool IsDirty 
        { 
            get => base.IsDirty || collection.Exists((X) => X.IsDirty);
            protected set => base.IsDirty = value; 
        }
        protected override void OnSaved()
        {
            base.OnSaved();
            collection.ForEach((X) => X.Saved());
        }

    }
    [Serializable]
    public class DirtyStructList<T> : BaseDirtyList<T> where T : struct { }
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