﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Xml;
using Kiss.Config;
using Kiss.Linq.Fluent;
using Kiss.Plugin;
using Kiss.Query;
using Kiss.Utils;

namespace Kiss.Linq.Sql
{
    public class Repository<T, t> : Repository<T>, IRepository<T, t>, IRepository<T>
        where T : Obj<t>, new()
    {
        #region ctor

        /// <summary>
        /// ctor
        /// </summary>
        public Repository()
        {
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="connectionStringSettings"></param>
        public Repository(ConnectionStringSettings connectionStringSettings)
            : base(connectionStringSettings)
        {
        }

        public Repository(string connstr_name)
            : base(connstr_name)
        {
        }

        #endregion

        //protected GetCacheKey getCacheKey = delegate(t i) { return string.Format("{0}:{1}", typeof(T).Name.ToLower(), i.ToString()); };

        ///// <summary>
        ///// Fine-grained using id
        ///// </summary>
        //public bool CacheIdGranularity { get; set; }

        public T Get(t id)
        {
            if (object.Equals(id, default(t)))
                return default(T);

            return (from obj in Query
                    where obj.Id.Equals(id)
                    select obj).FirstOrDefault();
        }

        public List<T> Gets(t[] ids)
        {
            if (ids.Length == 0)
                return new List<T>();

            List<t> idlist = new List<t>(ids);

            List<T> list = (from obj in Query
                            where new List<t>(ids).Contains(obj.Id)
                            select obj).ToList();

            list.Sort(delegate(T t1, T t2)
            {
                return idlist.IndexOf(t1.Id).CompareTo(idlist.IndexOf(t2.Id));
            });

            return list;
        }

        public void DeleteById(params t[] ids)
        {
            if (ids.Length == 0)
                return;

            foreach (t id in ids)
            {
                T obj = new T() { Id = id };

                Query.Add(obj);
                Query.Remove(obj);
            }

            Query.SubmitChanges(true);
        }

        public List<T> Gets(string commaDelimitedIds)
        {
            List<t> ids = new List<t>();

            foreach (var str in StringUtil.CommaDelimitedListToStringArray(commaDelimitedIds))
            {
                ids.Add(TypeConvertUtil.ConvertTo<t>(str));
            }

            return Gets(ids.ToArray());
        }

        public T Save(NameValueCollection param, ConvertObj<T> converter)
        {
            t id = default(t);
            if (StringUtil.HasText(param["id"]))
                id = TypeConvertUtil.ConvertTo<t>(param["id"]);

            T obj;

            if (object.Equals(id, default(t)))
            {
                obj = new T();
                Query.Add(obj);
            }
            else
            {
                Query.EnableQueryEvent = false;
                obj = Get(id);

                if (obj == null)// create a new record
                {
                    obj = new T();
                    obj.Id = id;
                    Query.Add(obj, true);
                }
            }

            if (!converter(obj, param))
                return null;

            Query.SubmitChanges(false);

            return obj;
        }

        public T Save(string param, ConvertObj<T> converter)
        {
            return Save(StringUtil.DelimitedEquation2NVCollection("&", param), converter);
        }
    }

    public class Repository<T> : Repository, IRepository<T>, IAutoStart where T : IQueryObject, new()
    {
        #region ctor

        /// <summary>
        /// ctor
        /// </summary>
        public Repository()
        {
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="connectionStringSettings"></param>
        public Repository(ConnectionStringSettings connectionStringSettings)
        {
            ConnectionStringSettings = connectionStringSettings;
        }

        public Repository(string connstr_name)
            : this(ConfigBase.GetConnectionStringSettings(connstr_name))
        {
        }

        #endregion

        private SqlQuery<T> _query;

        public IKissQueryable<T> Query
        {
            get
            {
                if (_query == null)
                    _query = CreateQuery() as SqlQuery<T>;
                return _query;
            }
        }

        /// <summary>
        /// create new linq query
        /// </summary>
        /// <returns></returns>
        public IKissQueryable<T> CreateQuery()
        {
            return new SqlQuery<T>(ConnectionStringSettings);
        }

        /// <summary>
        /// 获取对象列表
        /// </summary>
        /// <param name="qc"></param>
        /// <returns></returns>
        public List<T> Gets(QueryCondition q)
        {
            CheckQuery(q);

            if ((Query as SqlQuery<T>).DataContext == null)
                throw new LinqException("DataContext is null!");

            q.FireBeforeQueryEvent("Gets");

            if (string.IsNullOrEmpty(q.TableField))
                q.TableField = "*";

            if (q.PageSize == -1)
                q.PageSize = 20;

            string sql = q.WhereClause + q.PageIndex.ToString() + q.PageSize.ToString() + q.OrderByClause + q.TableField;

            Kiss.QueryObject.QueryEventArgs e = new Kiss.QueryObject.QueryEventArgs()
            {
                Type = typeof(T),
                Sql = sql
            };
            Kiss.QueryObject.OnPreQuery(e);

            if (e.Result != null)
                return e.Result as List<T>;

            List<T> list = new List<T>();

            using (IDataReader rdr = q.GetReader())
            {
                BucketImpl bucket = new BucketImpl<T>().Describe();

                while (rdr.Read())
                {
                    var item = new T();
                    var t = typeof(T);

                    FluentBucket.As(bucket).For.EachItem.Process(delegate(BucketItem bucketItem)
                    {
                        fillObject(rdr, item, t, bucketItem);
                    });

                    list.Add(item);
                }
            }

            Kiss.QueryObject.OnAfterQuery(new Kiss.QueryObject.QueryEventArgs()
            {
                Type = typeof(T),
                Sql = sql,
                Result = list
            });

            return list;
        }

        /// <summary>
        /// 获取记录数
        /// </summary>
        /// <param name="qc"></param>
        /// <returns></returns>
        public int Count(QueryCondition q)
        {
            CheckQuery(q);

            if ((Query as SqlQuery<T>).DataContext == null)
                throw new LinqException("DataContext is null!");

            q.FireBeforeQueryEvent("Count");

            string sql = "count" + q.WhereClause;

            Kiss.QueryObject.QueryEventArgs e = new Kiss.QueryObject.QueryEventArgs()
            {
                Type = typeof(T),
                Sql = sql
            };
            Kiss.QueryObject.OnPreQuery(e);

            if (e.Result != null)
                return (int)e.Result;

            int count = q.GetRelationCount();

            Kiss.QueryObject.OnAfterQuery(new Kiss.QueryObject.QueryEventArgs()
            {
                Type = typeof(T),
                Sql = sql,
                Result = count
            });

            return count;
        }

        public void Delete(QueryCondition q)
        {
            CheckQuery(q);

            q.FireBeforeQueryEvent("Delete");

            q.Delete();

            Kiss.QueryObject.OnBatch(typeof(T));
        }

        public T Save(T obj)
        {
            Query.SubmitChanges(false);

            return obj;
        }

        public List<T> GetsAll()
        {
            return (from q in Query
                    select q).ToList();
        }

        object IRepository.Gets(QueryCondition q)
        {
            return Gets(q);
        }

        private void CheckQuery(QueryCondition q)
        {
            if (q.ConnectionStringSettings == null)
                q.ConnectionStringSettings = ConnectionStringSettings;

            string tablename = Kiss.QueryObject<T>.GetTableName();

            if (string.IsNullOrEmpty(q.TableName))
                q.TableName = tablename;
        }

        private static void fillObject(IDataReader rdr, T item, Type t, BucketItem bucketItem)
        {
            PropertyInfo info = t.GetProperty(bucketItem.ProperyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            if (info != null && info.CanWrite)
            {
                object o = null;

                int index = rdr.GetOrdinal(bucketItem.Name);
                if (index != -1)
                {
                    o = rdr[index];
                    if (!(o is DBNull))
                    {
                        o = TypeConvertUtil.ConvertTo(o, info.PropertyType);

                        if (o != null)
                            info.SetValue(item
                                , o
                                , null);
                    }
                }
            }
        }

        #region IAutoStart Members

        public void Start()
        {
            CreatedEventArgs e = new CreatedEventArgs(typeof(T));
            e.ConnectionStringSettings = ConnectionStringSettings;

            OnCreated(e);

            if (e.ConnectionStringSettings != null)
                ConnectionStringSettings = e.ConnectionStringSettings;
        }

        #endregion
    }

    public class Repository
    {
        public ConnectionStringSettings ConnectionStringSettings { get; set; }

        public static event EventHandler<CreatedEventArgs> Created;

        protected void OnCreated(CreatedEventArgs e)
        {
            LoadConn(e);

            EventHandler<CreatedEventArgs> handler = Created;

            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void LoadConn(CreatedEventArgs e)
        {
            PluginSetting setting = PluginSettings.Get<RepositoryInitializer>();

            if (setting != null)
            {
                XmlNode connsnode = setting.Node.SelectSingleNode("conns");
                if (connsnode == null) return;

                ConnectionStringSettings = Config.ConfigBase.GetConnectionStringSettings(XmlUtil.GetStringAttribute(connsnode, "default", string.Empty));

                string tablename = Kiss.QueryObject.GetTableName(e.ModelType);

                foreach (XmlNode conn in connsnode.ChildNodes)
                {
                    string types = XmlUtil.GetStringAttribute(conn, "table", string.Empty);
                    if (StringUtil.IsNullOrEmpty(types))
                        continue;

                    bool match = false;

                    foreach (string type in StringUtil.Split(types, ",", true, true))
                    {
                        if (type.StartsWith("*") && tablename.EndsWith(type.Substring(1), StringComparison.InvariantCultureIgnoreCase))
                            match = true;

                        if (!match && type.EndsWith("*") && tablename.StartsWith(type.Substring(0, type.Length - 1), StringComparison.InvariantCultureIgnoreCase))
                            match = true;

                        if (!match && type.StartsWith("*") && type.EndsWith("*") && tablename.ToLower().Contains(type.Substring(1, type.Length - 1).ToLower()))
                            match = true;

                        if (!match && string.Equals(type, tablename, StringComparison.InvariantCultureIgnoreCase))
                            match = true;

                        if (match)
                            break;
                    }

                    if (match)
                    {
                        ConnectionStringSettings = Config.ConfigBase.GetConnectionStringSettings(XmlUtil.GetStringAttribute(conn, "conn", string.Empty));
                        break;
                    }
                }
            }
        }

        public class CreatedEventArgs : EventArgs
        {
            public ConnectionStringSettings ConnectionStringSettings { get; set; }

            public Type ModelType { get; private set; }

            public CreatedEventArgs(Type modelType)
            {
                ModelType = modelType;
            }
        }
    }
}
