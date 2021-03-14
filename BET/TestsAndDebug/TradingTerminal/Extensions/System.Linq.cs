using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Linq
{
    public static class Extensions
    {
        //public static IEnumerable<TSource> ForEachQuery<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        //{
        //    foreach (var item in source)
        //    {
        //        action(item);
        //        yield return item;
        //    }
        //}
        public static IEnumerable<TSource> ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        {
            foreach (var item in source)
                action(item);
            return source;
        }

        public static IEnumerable<TSource> ForEachMutable<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        {
            return source.ToArray().ForEach(action);
        }

        public static (TSource item, int index) FirstOrDefaultWithIndex<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var index = 0;
            foreach (var item in source)
            {
                if (predicate(item))
                    return (item, index);

                index++;
            }

            return (default, -1);
        }

        //public static IEnumerable<TResult> SelectWithPrevious<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TResult> projection)
        //{
        //    using (var iterator = source.GetEnumerator())
        //    {
        //        if (!iterator.MoveNext())
        //        {
        //            yield break;
        //        }
        //        TSource previous = iterator.Current;
        //        yield return projection(default, previous);
        //        while (iterator.MoveNext())
        //        {
        //            yield return projection(previous, iterator.Current);
        //            previous = iterator.Current;
        //        }
        //    }
        //}

        public static IEnumerable<TResult> SelectWithPreviousResult<TSource, TResult>(this IEnumerable<TSource> source, Func<TResult, TSource, TResult> projection)
        {
            using (var iterator = source.GetEnumerator())
            {
                if (!iterator.MoveNext())
                {
                    yield break;
                }
                TResult prevResult = projection(default, iterator.Current);
                yield return prevResult;
                while (iterator.MoveNext())
                {
                    prevResult = projection(prevResult, iterator.Current);
                    yield return prevResult;
                    //prevSource = iterator.Current;
                }
            }
        }
    }
}
