﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;

namespace Kiss.Linq.Sql.DataBase
{
    public class DDLFactory
    {
        // ddl status
        private static readonly List<string> ddl_status = new List<string>();
        private static readonly object _lock = new object();

        public static void Sync(IDDL ddl, Type objtype, ConnectionStringSettings css)
        {
            if (ddl == null)
                return;

            if (string.IsNullOrEmpty(css.Name))
                DDL(ddl, objtype, css, null);
            else
            {
                string key = objtype.Name + css.Name;

                if (!ddl_status.Contains(key))
                {
                    lock (_lock)
                    {
                        if (!ddl_status.Contains(key))
                        {
                            DDL(ddl, objtype, css, key);
                        }
                    }
                }
            }
        }

        private static void DDL(IDDL ddl, Type objtype, ConnectionStringSettings css, string key)
        {
            try
            {
                Database db = new Database(ddl, css.ConnectionString);

                StringBuilder sql = new StringBuilder();

                // get db's table and column
                db.Fill();

                sql.Append(db.GenerateSql(objtype));

                // execute sql
                db.Execute(sql.ToString());

                LogManager.GetLogger<DDLFactory>().Info("sync table schema of {0} ok.", objtype.Name);
            }
            catch (Exception ex)
            {
                LogManager.GetLogger<DDLFactory>().Fatal("Try to sync Model: "+objtype.Name+" schema ERROR："+ex.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(css.Name))
                    if (!ddl_status.Contains(key))
                        ddl_status.Add(key);
            }
        }
    }
}
