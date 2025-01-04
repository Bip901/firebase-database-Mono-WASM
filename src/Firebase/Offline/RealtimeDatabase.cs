namespace Firebase.Database.Offline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Firebase.Database.Extensions;
    using Firebase.Database.Query;
    using Firebase.Database.Streaming;
    using System.Linq.Expressions;
    using Internals;
    using Newtonsoft.Json;
    using System.Reflection;

    /// <summary>
    /// The real-time Database which synchronizes online and offline data. 
    /// </summary>
    /// <typeparam name="T"> Type of entities. </typeparam>
    public partial class RealtimeDatabase<T> : IDisposable where T : class
    {
        private readonly ChildQuery childQuery;
        private readonly string elementRoot;
        private readonly bool pushChanges;
        private readonly FirebaseCache<T> firebaseCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="RealtimeDatabase{T}"/> class.
        /// </summary>
        /// <param name="childQuery"> The child query.  </param>
        /// <param name="elementRoot"> The element Root. </param>
        /// <param name="offlineDatabaseFactory"> The offline database factory.  </param>
        /// <param name="filenameModifier"> Custom string which will get appended to the file name.  </param>
        /// <param name="streamingOptions"> Specifies condition for which items get streamed. </param>
        /// <param name="initialPullStrategy"> Specifies the strategy for initial pull of server data. </param>
        /// <param name="pushChanges"> Specifies whether changed items should actually be pushed to the server. If this is false, then Put / Post / Delete will not affect server data. </param>
        /// <param name="setHandler"></param>
        public RealtimeDatabase(ChildQuery childQuery, string elementRoot, Func<Type, string, IDictionary<string, OfflineEntry>> offlineDatabaseFactory, string filenameModifier, StreamingOptions streamingOptions, InitialPullStrategy initialPullStrategy, bool pushChanges, ISetHandler<T> setHandler = null)
        {
            this.childQuery = childQuery;
            this.elementRoot = elementRoot;
            this.pushChanges = pushChanges;
            this.Database = offlineDatabaseFactory(typeof(T), filenameModifier);
            this.firebaseCache = new FirebaseCache<T>(new OfflineCacheAdapter<string, T>(this.Database));

            this.PutHandler = setHandler ?? new SetHandler<T>();

            Task.Factory.StartNew(this.SynchronizeThread, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Event raised whenever an exception is thrown in the synchronization thread. Exception thrown in there are swallowed, so this event is the only way to get to them. 
        /// </summary>
        public event EventHandler<ExceptionEventArgs> SyncExceptionThrown;

        /// <summary>
        /// Gets the backing Database.
        /// </summary>
        public IDictionary<string, OfflineEntry> Database
        {
            get;
            private set;
        }

        public ISetHandler<T> PutHandler
        {
            private get;
            set;
        }

        /// <summary>
        /// Overwrites existing object with given key.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="obj"> The object to set. </param>
        /// <param name="syncOptions"> Specifies type of sync requested for given data. </param>
        /// <param name="priority"> The priority. Objects with higher priority will be synced first. Higher number indicates higher priority. </param>
        public void Set(string key, T obj, SyncOptions syncOptions, int priority = 1)
        {
            this.SetAndRaise(key, new OfflineEntry(key, obj, priority, syncOptions));
        }

        public void Set<TProperty>(string key, Expression<Func<T, TProperty>> propertyExpression, object value, SyncOptions syncOptions, int priority = 1)
        {
            var fullKey = this.GenerateFullKey(key, propertyExpression, syncOptions);
            var serializedObject = JsonConvert.SerializeObject(value).Trim('"', '\\');

            if (fullKey.Item3)
            {
                if (typeof(TProperty) != typeof(string) || value == null)
                {
                    // don't escape non-string primitives and null;
                    serializedObject = $"{{ \"{fullKey.Item2}\" : {serializedObject} }}";
                }
                else
                {
                    serializedObject = $"{{ \"{fullKey.Item2}\" : \"{serializedObject}\" }}";
                }
            }

            var setObject = this.firebaseCache.PushData("/" + fullKey.Item1, serializedObject).First();

            if (!this.Database.ContainsKey(key) || this.Database[key].SyncOptions != SyncOptions.Patch && this.Database[key].SyncOptions != SyncOptions.Put)
            {
                this.Database[fullKey.Item1] = new OfflineEntry(fullKey.Item1, value, serializedObject, priority, syncOptions, true);
            }
        }

        /// <summary>
        /// Fetches an object with the given key and adds it to the Database.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="priority"> The priority. Objects with higher priority will be synced first. Higher number indicates higher priority. </param>
        public void Pull(string key, int priority = 1)
        {
            if (!this.Database.ContainsKey(key))
            {
                this.Database[key] = new OfflineEntry(key, null, priority, SyncOptions.Pull);
            }
            else if (this.Database[key].SyncOptions == SyncOptions.None)
            {
                // pull only if push isn't pending
                this.Database[key].SyncOptions = SyncOptions.Pull;
            }
        }

        /// <summary>
        /// Retrieves all offline items currently stored in local database.
        /// </summary>
        public IEnumerable<FirebaseObject<T>> Once()
        {
            return this.Database
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value.Data) && kvp.Value.Data != "null" && !kvp.Value.IsPartial)
                .Select(kvp => new FirebaseObject<T>(kvp.Key, kvp.Value.Deserialize<T>()))
                .ToList();
        }

        public void Dispose()
        {
        }

        private void SetAndRaise(string key, OfflineEntry obj, FirebaseEventSource eventSource = FirebaseEventSource.Offline)
        {
            this.Database[key] = obj;
            obj?.Deserialize<T>();
        }

        private async void SynchronizeThread()
        {
            while (true)
            {
                try
                {
                    var validEntries = this.Database.Where(e => e.Value != null);
                    await PullEntriesAsync(validEntries.Where(kvp => kvp.Value.SyncOptions == SyncOptions.Pull)).ConfigureAwait(false);

                    if (this.pushChanges)
                    {
                        await PushEntriesAsync(validEntries.Where(kvp => kvp.Value.SyncOptions == SyncOptions.Put || kvp.Value.SyncOptions == SyncOptions.Patch)).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    this.SyncExceptionThrown?.Invoke(this, new ExceptionEventArgs(ex));
                }

                await TaskDelayProvider.Constructor(childQuery.Client.Options.SyncPeriod).ConfigureAwait(false);
            }
        }

        private async Task PushEntriesAsync(IEnumerable<KeyValuePair<string, OfflineEntry>> pushEntries)
        {
            var groups = pushEntries.GroupBy(pair => pair.Value.Priority).OrderByDescending(kvp => kvp.Key).ToList();

            foreach (var group in groups)
            {
                var tasks = group.OrderBy(kvp => kvp.Value.IsPartial).Select(kvp => 
                    kvp.Value.IsPartial ?
                    this.ResetSyncAfterPush(this.PutHandler.SetAsync(this.childQuery, kvp.Key, kvp.Value), kvp.Key) :
                    this.ResetSyncAfterPush(this.PutHandler.SetAsync(this.childQuery, kvp.Key, kvp.Value), kvp.Key, kvp.Value.Deserialize<T>()));

                try
                {
                    await Task.WhenAll(tasks).WithAggregateException().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.SyncExceptionThrown?.Invoke(this, new ExceptionEventArgs(ex));
                }
            }
        }

        private async Task PullEntriesAsync(IEnumerable<KeyValuePair<string, OfflineEntry>> pullEntries)
        {
            var taskGroups = pullEntries.GroupBy(pair => pair.Value.Priority).OrderByDescending(kvp => kvp.Key);

            foreach (var group in taskGroups)
            {
                var tasks = group.Select(pair => this.ResetAfterPull(this.childQuery.Child(pair.Key == this.elementRoot ? string.Empty : pair.Key).OnceSingleAsync<T>(), pair.Key, pair.Value));

                try
                { 
                    await Task.WhenAll(tasks).WithAggregateException().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.SyncExceptionThrown?.Invoke(this, new ExceptionEventArgs(ex));
                }
            }
        }

        private async Task ResetAfterPull(Task<T> task, string key, OfflineEntry entry)
        {
            await task.ConfigureAwait(false);
            this.SetAndRaise(key, new OfflineEntry(key, task.Result, entry.Priority, SyncOptions.None), FirebaseEventSource.OnlinePull);
        }

        private async Task ResetSyncAfterPush(Task task, string key, T obj)
        {
            await ResetSyncAfterPush(task, key).ConfigureAwait(false);
        }

        private async Task ResetSyncAfterPush(Task task, string key)
        {
            await task.ConfigureAwait(false);
            this.ResetSyncOptions(key);
        }

        private void ResetSyncOptions(string key)
        {
            var item = this.Database[key];

            if (item.IsPartial)
            {
                this.Database.Remove(key);
            }
            else
            {
                item.SyncOptions = SyncOptions.None;
                this.Database[key] = item;
            }
        }

        private Tuple<string, string, bool> GenerateFullKey<TProperty>(string key, Expression<Func<T, TProperty>> propertyGetter, SyncOptions syncOptions)
        {
            var visitor = new MemberAccessVisitor();
            visitor.Visit(propertyGetter);
            var propertyType = typeof(TProperty).GetTypeInfo();
            var prefix = key == string.Empty ? string.Empty : key + "/";

            // primitive types
            if (syncOptions == SyncOptions.Patch && (propertyType.IsPrimitive || Nullable.GetUnderlyingType(typeof(TProperty)) != null || typeof(TProperty) == typeof(string)))
            {
                return Tuple.Create(prefix + string.Join("/", visitor.PropertyNames.Skip(1).Reverse()), visitor.PropertyNames.First(), true);
            }

            return Tuple.Create(prefix + string.Join("/", visitor.PropertyNames.Reverse()), visitor.PropertyNames.First(), false);
        }

    }
}
