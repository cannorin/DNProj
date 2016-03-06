//The X11 License
//NX - extension objects and methods of C#
//Copyright(c) 2015 cannorin

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.Serialization;

namespace NX
{
    public class TComparer<T> : IComparer<T>
    {
        public Func<T, T, int> Comparer { get; private set; }

        public TComparer(Func<T, T, int> f)
        {
            Comparer = f;
        }

        public int Compare(T x, T y)
        {
            return Comparer(x, y);
        }
    }

    public static class ExprNX
    {
        public static T Block<T>(Func<T> f)
        {
            return f();
        }
    }

    public static class IENX
    {
        public static IEnumerable<string> EnumerateLines(this StreamReader sr)
        {
            while (!sr.EndOfStream)
                yield return sr.ReadLine();
        }

        public static string JoinToString<T>(this IEnumerable<T> source)
        {
            return string.Concat(source);
        }

        public static string JoinToString<T>(this IEnumerable<T> source, string separator)
        {
            return string.Join(separator, source);
        }

        public static IEnumerable<T> Singleton<T>(this T t)
        {
            return new[] { t };
        }

        public static IEnumerable<int> ZeroTo(int n)
        {
            return Init(_ => _, n);
        }

        public static IEnumerable<T> Init<T>(Func<int, T> f, int n = int.MaxValue)
        {
            return Enumerable.Range(0, n).Map(f);
        }

        public static IEnumerable<T> Repeat<T>(T a, int n = int.MaxValue)
        {
            if (n == int.MaxValue)
                return repeatInf(a);
            else
                return Enumerable.Repeat(a, n);
        }

        public static IEnumerable<bool> Inf(bool b = false)
        {
            return repeatInf(b);
        }

        static IEnumerable<T> repeatInf<T>(T a)
        {
            while (true)
                yield return a;
        }

        public static int LengthNX<T>(this IEnumerable<T> seq)
        {
            return seq.Count();
        }

        public static long LongLengthNX<T>(this IEnumerable<T> seq)
        {
            return seq.LongCount();
        }

        public static T Hd<T>(this IEnumerable<T> seq)
        {
            return seq.First();
        }

        public static T Tl<T>(this IEnumerable<T> seq)
        {
            return seq.Last();
        }

        public static T Nth<T>(this IEnumerable<T> seq, int n)
        {
            if (n < seq.Count() - 1)
                throw new IndexOutOfRangeException();
            else
                return seq.Skip(n).First();
        }

        public static int IndexOf<T>(this IEnumerable<T> seq, T a)
        {
            var i = 0;
            var comparer = EqualityComparer<T>.Default;
            foreach (T item in seq)
            {
                if (comparer.Equals(item, a))
                    return i;
                i++;
            }
            return -1;
        }

        public static IEnumerable<T> Rev<T>(this IEnumerable<T> seq)
        {
            return seq.Reverse();
        }

        public static IEnumerable<T> Append<T>(this IEnumerable<T> s1, IEnumerable<T> s2)
        {
            return s1.Concat(s2);
        }

        public static IEnumerable<T> RevAppend<T>(this IEnumerable<T> s1, IEnumerable<T> s2)
        {
            return s1.Reverse().Concat(s2);
        }

        public static IEnumerable<T> ConcatNX<T>(this IEnumerable<IEnumerable<T>> seq)
        {
            return seq.SelectMany(x => x);
        }

        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> seq)
        {
            return seq.SelectMany(x => x);
        }


        public static IEnumerable<T2> Map<T1, T2>(this IEnumerable<T1> seq, Func<T1, T2> f)
        {
            return seq.Select(f);
        }

        public static IEnumerable<T2> MapI<T1, T2>(this IEnumerable<T1> seq, Func<T1, int, T2> f)
        {
            return seq.Select(f);
        }

        public static void Iter<T>(this IEnumerable<T> seq, Action<T> f)
        {
            foreach (var x in seq)
                f(x);
        }

        public static T FoldL<T>(this IEnumerable<T> seq, Func<T, T, T> f)
        {
            return seq.Aggregate(f);
        }

        public static T FoldR<T>(this IEnumerable<T> seq, Func<T, T, T> f)
        {
            return seq.Reverse().Aggregate(f);
        }

        public static TR FoldL<T, TR>(this IEnumerable<T> seq, Func<TR, T, TR> f, TR a)
        {
            return seq.Aggregate(a, f);
        }

        public static TR FoldR<T, TR>(this IEnumerable<T> seq, Func<TR, T, TR> f, TR a)
        {
            return seq.Reverse().Aggregate(a, f);
        }

        public static IEnumerable<TR> Unfold<T, TR>(this T s, Func<T, Tuple<TR, T>> n)
        {
            T x = s;
            while (true)
            {
                var y = n(x);
                yield return y.Item1;
                x = y.Item2;
            }
        }

        public static void Iter2<T1, T2>(this IEnumerable<T1> s1, IEnumerable<T2> s2, Action<T1, T2> f)
        {
            var e1 = s1.GetEnumerator();
            var e2 = s2.GetEnumerator();
            bool b1, b2;
            while ((b1 = e1.MoveNext()) & (b2 = e2.MoveNext()))
                f(e1.Current, e2.Current);

            if (b1 != b2)
                throw new ArgumentOutOfRangeException("Length not match");
        }

        public static IEnumerable<T3> Map2<T1, T2, T3>(this IEnumerable<T1> s1, IEnumerable<T2> s2, Func<T1, T2, T3> f)
        {
            var e1 = s1.GetEnumerator();
            var e2 = s2.GetEnumerator();
            bool b1, b2;
            while ((b1 = e1.MoveNext()) & (b2 = e2.MoveNext()))
                yield return f(e1.Current, e2.Current);

            if (b1 != b2)
                throw new ArgumentOutOfRangeException("Length not match");
        }

        public static IEnumerable<T3> Map2I<T1, T2, T3>(this IEnumerable<T1> s1, IEnumerable<T2> s2, Func<T1, T2, int, T3> f)
        {
            var e1 = s1.GetEnumerator();
            var e2 = s2.GetEnumerator();
            var i = 0;
            bool b1, b2;
            while ((b1 = e1.MoveNext()) & (b2 = e2.MoveNext()))
            {
                yield return f(e1.Current, e2.Current, i);
                i++;
            }
            if (b1 != b2)
                throw new ArgumentOutOfRangeException("Length not match");
        }

        public static T3 FoldL2<T1, T2, T3>(this IEnumerable<T1> s1, IEnumerable<T2> s2, Func<T3, T1, T2, T3> f, T3 a)
        {
            var e1 = s1.GetEnumerator();
            var e2 = s2.GetEnumerator();
            var e3 = a;
            bool b1, b2;
            while ((b1 = e1.MoveNext()) & (b2 = e2.MoveNext()))
                e3 = f(e3, e1.Current, e2.Current);

            if (b1 != b2)
                throw new ArgumentOutOfRangeException();
            return e3;
        }

        public static T3 FoldR2<T1, T2, T3>(this IEnumerable<T1> s1, IEnumerable<T2> s2, Func<T3, T1, T2, T3> f, T3 a)
        {
            return s1.Reverse().FoldL2(s2.Reverse(), f, a);
        }


        public static T Find<T>(this IEnumerable<T> seq, Func<T, bool> f)
        {
            return seq.First(f);
        }

        public static IEnumerable<T> Filter<T>(this IEnumerable<T> seq, Func<T, bool> f)
        {
            return seq.Where(f);
        }

        public static Tuple<IEnumerable<T>, IEnumerable<T>> Partition<T>(this IEnumerable<T> seq, Func<T, bool> f)
        {
            return Tuple.Create(seq.Where(f), seq.Where(x => !f(x)));
        }

        public static T2 Assoc<T1, T2>(this IEnumerable<Tuple<T1, T2>> seq, T1 a)
        {
            return seq.First(x => x.Item1.Equals(a)).Item2;
        }

        public static T2 Assoc<T1, T2>(this IEnumerable<KeyValuePair<T1, T2>> seq, T1 a)
        {
            return seq.First(x => x.Key.Equals(a)).Value;
        }

        public static bool MemAssoc<T1, T2>(this IEnumerable<Tuple<T1, T2>> seq, T1 a)
        {
            return seq.Any(x => x.Item1.Equals(a));
        }

        public static bool MemAssoc<T1, T2>(this IEnumerable<KeyValuePair<T1, T2>> seq, T1 a)
        {
            return seq.Any(x => x.Key.Equals(a));
        }

        public static IEnumerable<Tuple<T1, T2>> RemoveAssoc<T1, T2>(this IEnumerable<Tuple<T1, T2>> seq, T1 a)
        {
            var found = false;
            return seq.SkipWhile(x => !found && (found = x.Item1.Equals(a)));
        }

        public static IEnumerable<KeyValuePair<T1, T2>> RemoveAssoc<T1, T2>(this IEnumerable<KeyValuePair<T1, T2>> seq, T1 a)
        {
            var found = false;
            return seq.SkipWhile(x => !found && (found = x.Key.Equals(a)));
        }

        public static Tuple<IEnumerable<T1>, IEnumerable<T2>> SplitNX<T1, T2>(this IEnumerable<Tuple<T1, T2>> seq)
        {
            return Tuple.Create(seq.Map(x => x.Item1), seq.Map(x => x.Item2));
        }

        public static KeyValuePair<IEnumerable<T1>, IEnumerable<T2>> SplitNX<T1, T2>(this IEnumerable<KeyValuePair<T1, T2>> seq)
        {
            return new KeyValuePair<IEnumerable<T1>, IEnumerable<T2>>(seq.Map(x => x.Key), seq.Map(x => x.Value));
        }

        public static IEnumerable<Tuple<T1, T2>> CombineNX<T1, T2>(this Tuple<IEnumerable<T1>, IEnumerable<T2>> seq)
        {
            return CombineTupleNX(seq.Item1, seq.Item2);
        }

        public static IEnumerable<KeyValuePair<T1, T2>> CombineNX<T1, T2>(this KeyValuePair<IEnumerable<T1>, IEnumerable<T2>> seq)
        {
            return CombineKvpNX(seq.Key, seq.Value);
        }

        public static IEnumerable<KeyValuePair<T1, T2>> CombineNX<T1, T2>(this IEnumerable<T1> s1, IEnumerable<T2> s2)
        {
            return s1.Map2(s2, (x, y) => new KeyValuePair<T1, T2>(x, y));
        }

        public static IEnumerable<Tuple<T1, T2>> CombineTupleNX<T1, T2>(this IEnumerable<T1> s1, IEnumerable<T2> s2)
        {
            return s1.Map2(s2, (x, y) => Tuple.Create(x, y));
        }

        public static IEnumerable<KeyValuePair<T1, T2>> CombineKvpNX<T1, T2>(this IEnumerable<T1> s1, IEnumerable<T2> s2)
        {
            return s1.Map2(s2, (x, y) => new KeyValuePair<T1, T2>(x, y));
        }

        public static IEnumerable<T> Sort<T>(this IEnumerable<T> seq, Func<T, T, int> f)
        {
            return seq.OrderBy(x => x, new TComparer<T>(f));
        }

        public static IEnumerable<T> Sort<T>(this IEnumerable<T> seq)
        {
            return seq.OrderBy(x => x);
        }
    }

    public static class EnumNX
    {
        public static T Parse<T>(object s)
        {
            return (T)Enum.Parse(typeof(T), s.ToString());
        }
    }

    public static class IDisposableNX
    {
        public class Using<T>
            where T : IDisposable
        {
            public T Source { get; set; }

            public Using(T source)
            {
                Source = source;
            }
        }

        public static Using<T> Use<T>(this T source)
            where T : IDisposable
        {
            return new Using<T>(source);
        }

        public static TR Map<T, TR>(this Using<T> source, Func<T, TR> selector)
            where T : IDisposable
        {
            using (source.Source)
                return selector(source.Source);
        }

        public static TR Map2<T1, T2, TR>(this Using<T1> a, Using<T2> b, Func<T1, T2, TR> f)
            where T1 : IDisposable
            where T2 : IDisposable
        {
            using (a.Source)
            using (b.Source)
                return f(a.Source, b.Source);
        }

        public static TR SelectMany<T, T2, TR>
        (this Using<T> source, Func<T, Using<T2>> second, Func<T, T2, TR> selector)
            where T : IDisposable
            where T2 : IDisposable
        {
            return Map2(source, second(source.Source), selector);
        }

        public static void SelectMany<T, T2>
        (this Using<T> source, Func<T, Using<T2>> second, Action<T, T2> selector)
            where T : IDisposable
            where T2 : IDisposable
        {
            Map2(source, second(source.Source), (x, y) =>
                {
                    selector(x, y);
                    return Unit.Value;
                });
        }

        public static TR Select<T, TR>(this Using<T> source, Func<T, TR> selector)
            where T : IDisposable
        {
            return source.Map(selector);
        }
    }

    public struct Option<T> : IEquatable<Option<T>>
    {
        public bool HasValue { get; set; }

        public T Value { get; set; }

        public bool HasException { get; set; }

        public Exception InnerException { get; set; }

        public Option(T a = default(T))
        {
            if (a is Object && a == null)
            {
                this.HasValue = this.HasException = false;
                this.Value = default(T);
                this.InnerException = null;
            }
            else
            {
                this.HasValue = true;
                this.Value = a;
                this.InnerException = null;
                this.HasException = false;
            }
        }

        public Option(Exception e)
        {
            this.HasValue = false;
            this.HasException = true;
            this.Value = default(T);
            this.InnerException = e;
        }

        public static implicit operator Option<T>(Option<DummyNX> d)
        {
            return new Option<T>();
        }

        public bool Equals(Option<T> other)
        {
            if (this.HasValue && other.HasValue)
                return EqualityComparer<T>.Default.Equals(this.Value, other.Value);
            else if (!this.HasValue && !other.HasValue)
                return true;
            else
                return false;
        }

        public bool WeakEquals(Option<T> other)
        {
            return this.Equals(other);
        }

        public bool WeakEquals<T2>(Option<T2> other)
        {
            if (!this.HasValue && !other.HasValue)
                return true;
            else
                return false;
        }

        public override bool Equals(object obj)
        {
            if (obj is Option<T>)
                return ((Option<T>)obj).Equals(this);
            else
                return false;
        }

        public override int GetHashCode()
        {
            /*
                _____                                          __           ___________.__              ____ ___.__   __  .__                __           ________                          __  .__                     
               /  _  \   ____   ________  _  __ ___________  _/  |_  ____   \__    ___/|  |__   ____   |    |   \  |_/  |_|__| _____ _____ _/  |_  ____   \_____  \  __ __   ____   _______/  |_|__| ____   ____        
              /  /_\  \ /    \ /  ___/\ \/ \/ // __ \_  __ \ \   __\/  _ \    |    |   |  |  \_/ __ \  |    |   /  |\   __\  |/     \\__  \\   __\/ __ \   /  / \  \|  |  \_/ __ \ /  ___/\   __\  |/  _ \ /    \       
             /    |    \   |  \\___ \  \     /\  ___/|  | \/  |  | (  <_> )   |    |   |   Y  \  ___/  |    |  /|  |_|  | |  |  Y Y  \/ __ \|  | \  ___/  /   \_/.  \  |  /\  ___/ \___ \  |  | |  (  <_> )   |  \      
             \____|__  /___|  /____  >  \/\_/  \___  >__|     |__|  \____/    |____|   |___|  /\___  > |______/ |____/__| |__|__|_|  (____  /__|  \___  > \_____\ \_/____/  \___  >____  > |__| |__|\____/|___|  /      
                     \/     \/     \/              \/                                       \/     \/                              \/     \/          \/         \__>           \/     \/                      \/       
                                                                                                              _____                                                                                                     
                                                                                                        _____/ ____\                                                                                                    
                                                                                                       /  _ \   __\                                                                                                     
                                                                                                      (  <_> )  |                                                                                                       
                                                                                                       \____/|__|                                                                                                       

             .____    .__  _____              __  .__              ____ ___      .__                                                          .___ ___________                            __  .__    .__                
             |    |   |__|/ ____\____       _/  |_|  |__   ____   |    |   \____ |__|__  __ ___________  ______ ____       _____    ____    __| _/ \_   _____/__  __ ___________ ___.__._/  |_|  |__ |__| ____    ____  
             |    |   |  \   __\/ __ \      \   __\  |  \_/ __ \  |    |   /    \|  \  \/ // __ \_  __ \/  ___// __ \      \__  \  /    \  / __ |   |    __)_\  \/ // __ \_  __ <   |  |\   __\  |  \|  |/    \  / ___\ 
             |    |___|  ||  | \  ___/       |  | |   Y  \  ___/  |    |  /   |  \  |\   /\  ___/|  | \/\___ \\  ___/       / __ \|   |  \/ /_/ |   |        \\   /\  ___/|  | \/\___  | |  | |   Y  \  |   |  \/ /_/  >
             |_______ \__||__|  \___  > /\   |__| |___|  /\___  > |______/|___|  /__| \_/  \___  >__|  /____  >\___  > /\  (____  /___|  /\____ |  /_______  / \_/  \___  >__|   / ____| |__| |___|  /__|___|  /\___  / 
                     \/             \/  )/             \/     \/               \/              \/           \/     \/  )/       \/     \/      \/          \/           \/       \/                \/        \//_____/  
            */

            /*                                                                                   __/\/\/\/\/\/\/\/\/\__                                                                                                */
            /*                                                                                   >                    <                                                                                                */
            return (/*                                                                           >       */42/*       <                                                                                                */ * 10 + 1) ^ this.HasValue.GetHashCode() ^ (this.HasValue ? 0 : Value.GetHashCode());
            /*                                                                                   >                    <                                                                                                */
            /*                                                                                   ‾^Y^Y^Y^Y^Y^Y^Y^Y^Y^Y‾                                                                                                */
        }

        public static bool operator ==(Option<T> a, Option<T> b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Option<T> a, Option<T> b)
        {
            return !a.Equals(b);
        }
    }

    public sealed class DummyNX
    {

    }

    public sealed class Unit : IEquatable<Unit>
    {
        public override bool Equals(object obj)
        {
            return obj is Unit;
        }

        public bool Equals(Unit o)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString()
        {
            return "";
        }

        public static Unit Value
        {
            get
            {
                return new Unit();
            }
        }
    }

    public static class OptionNX
    {
        public static Option<T> Option<T>(this T a)
        {
            return new Option<T>(a);
        }

        public static Option<Unit> Option()
        {
            return Option(Unit.Value);
        }

        public static Option<DummyNX> None
        {
            get
            {
                return new Option<DummyNX>();
            }
        }

        public static void May<T>(this Option<T> o, Action<T> f)
        {
            if (o.HasValue)
                f(o.Value);
        }

        public static Option<T2> Map<T, T2>(this Option<T> a, Func<T, T2> f)
        {
            return a.HasValue ? Option(f(a.Value)) : None;
        }

        public static Option<T2> Select<T, T2>(this Option<T> a, Func<T, T2> f)
        {
            return a.Map(f);
        }

        public static Option<TR> Map2<T1, T2, TR>(this Option<T1> a, Option<T2> b, Func<T1, T2, TR> f)
        {
            return (a.HasValue && b.HasValue) ? Option(f(a.Value, b.Value)) : None;
        }

        public static Option<TR> SelectMany<T1, T2, TR>(this Option<T1> a, Func<T1, Option<T2>> bf, Func<T1, T2, TR> f)
        {
            return a.HasValue ? a.Map2(bf(a.Value), f) : None;
        }

        public static Option<T2> Bind<T, T2>(this Option<T> a, Func<T, Option<T2>> f)
        {
            return a.HasValue ? f(a.Value) : None;
        }

        public static T Default<T>(this Option<T> a, T b)
        {
            return a.HasValue ? a.Value : b;
        }

        public static T DefaultLazy<T>(this Option<T> a, Func<T> b)
        {
            return a.HasValue ? a.Value : b();
        }

        public static T2 MapDefault<T, T2>(this Option<T> a, Func<T, T2> f, T2 b)
        {
            return a.HasValue ? f(a.Value) : b;
        }

        public static TR Match<T, TR>(this Option<T> a, Func<T, TR> Some, Func<TR> None)
        {
            return a.HasValue ? Some(a.Value) : None();
        }

        public static void Match<T>(this Option<T> a, Action<T> Some, Action None)
        {
            if (a.HasValue)
                Some(a.Value);
            else
                None();
        }

        public static TR MatchEx<T, TR>(this Option<T> a, Func<T, TR> Some, Func<Option<Exception>, TR> None)
        {
            return a.HasValue ? Some(a.Value) : None(a.HasException ? Option(a.InnerException) : OptionNX.None);
        }

        public static void MatchEx<T>(this Option<T> a, Action<T> Some, Action<Option<Exception>> None)
        {
            if (a.HasValue)
                Some(a.Value);
            else
                None(a.HasException ? Option(a.InnerException) : OptionNX.None);
        }

        public static Option<TR> Try<T, TR>(this T t, Func<T, TR> f)
        {
            return Try(() => f(t));
        }

        public static Option<TR> Try<T, TR>(this Option<T> t, Func<T, TR> f)
        {
            return !t.HasValue ?
                t.HasException ?
                    new Option<TR>(t.InnerException) :
                    new Option<TR>(new NullReferenceException("This is None<" + typeof(T).Name + ">"))
            : Try(() => f(t.Value));
        }

        public static Option<T> Try<T>(this Func<T> f)
        {
            try
            {
                return Option(f());
            }
            catch (Exception e)
            {
                return new Option<T>(e);
            }
        }

        public static Option<Unit> Try<T>(this T t, Action<T> f)
        {
            return Try(() => f(t));
        }

        public static Option<Unit> Try<T>(this Option<T> t, Action<T> f)
        {
            return !t.HasValue ? 
                t.HasException ? 
                    new Option<Unit>(t.InnerException) :
                    new Option<Unit>(new NullReferenceException("This is None<" + typeof(T).Name + ">"))
                : Try(() => f(t.Value));
        }

        public static Option<Unit> Try(this Action f)
        {
            try
            {
                f();
                return Option(Unit.Value);
            }
            catch (Exception e)
            {
                return new Option<Unit>(e);
            }
        }
    }

    public static class StreamNX
    {
        public static void WriteString(this Stream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteString(this Stream stream, string value, Encoding enc)
        {
            var bytes = enc.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}