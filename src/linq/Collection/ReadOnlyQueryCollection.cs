﻿using System.Collections.Generic;
using System.Linq;

namespace Kiss.Linq
{
    public abstract class ReadOnlyQueryCollection<T> : IQuery<T>, IQuery
    {
        /// <summary>
        /// collection items 
        /// </summary>
        protected List<T> Items = new List<T> ( );

        #region IQueryReadOnly<T> Members

        /// <summary>
        /// Returns a single item from the collection.
        /// </summary>
        /// <returns></returns>
        T IQuery<T>.Single ( )
        {
            return Items.Single ( );
        }

        /// <summary>
        /// Returns a single item or default value if empty.
        /// </summary>
        /// <returns></returns>
        public T SingleOrDefault ( )
        {
            if ( Items.Count == 1 )
                return Items.Single ( );
            if ( Items.Count > 1 )
                throw new LinqException ( Properties.Resource.MultipleElementInColleciton );
            return default ( T );
        }

        /// <summary>
        /// Return true if there is any item in collection.
        /// </summary>
        /// <returns></returns>
        public bool Any ( )
        {
            return Items.Count > 0;
        }

        /// <summary>
        /// Returns the count of items in the collection.
        /// </summary>
        /// <returns></returns>
        public object Count ( )
        {
            return Items.Count;
        }

        /// <summary>
        /// Returns the first item from the collection.
        /// </summary>
        /// <returns></returns>
        T IQuery<T>.First ( )
        {
            return Items.First ( );
        }

        /// <summary>
        /// Returns first item or default value if empty.
        /// </summary>
        /// <returns></returns>
        public T FirstOrDefault ( )
        {
            if ( Items.Count > 0 )
                return Items.First ( );
            return default ( T );
        }

        /// <summary>
        /// Returns the last item from the collection.
        /// </summary>
        /// <returns></returns>
        T IQuery<T>.Last ( )
        {
            return Items.Last ( );
        }

        /// <summary>
        /// Returns last item or default value if empty.
        /// </summary>
        /// <returns></returns>
        public T LastOrDefault ( )
        {
            if ( Items.Count > 0 )
                return Items.Last ( );
            return default ( T );
        }

        #endregion
    }
}
