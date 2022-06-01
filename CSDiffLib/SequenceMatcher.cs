using System.Xml.Serialization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Reflection.Metadata;
using System.ComponentModel.Design.Serialization;
using System.Security.Cryptography.X509Certificates;

namespace CSDiffLib
{

    public record Match(int aIndex, int bIndex, int Length) : IComparable<Match>
    {
        public int CompareTo(Match? other)
        {
            if (aIndex != other.aIndex) return aIndex.CompareTo(other.aIndex);
            if (bIndex != other.bIndex) return bIndex.CompareTo(other.bIndex);
            return Length.CompareTo(other.Length);
        }

        public override string ToString()
        {
            return $"<Match a: {aIndex} b: {bIndex} Length: {Length}>";
        }
    }

    public enum OpTags {Replace, Delete, Insert, Equal, Empty};

    public record OpCode(OpTags tag, int aBegin, int aEnd, int bBegin, int bEnd)
    {
        public override string ToString()
        {
            return $"<OpCode {tag.ToString()} a[{aBegin}:{aEnd}] b[{bBegin}:{bEnd}] >";
        }

        public string ToString<T>(List<T> a, List<T> b, string joinStr = "")
        {
            string a_sub = String.Join(joinStr,
                a.Skip(aBegin).Take(aEnd - aBegin).Select(x => x.ToString()));
            string b_sub = String.Join(joinStr,
                b.Skip(bBegin).Take(bEnd - bBegin).Select(x => x.ToString()));
            return $"<OpCode {tag.ToString()} a[{aBegin}:{aEnd}] ({a_sub}) b[{bBegin}:{bEnd}] ({b_sub}) >";
        }
    }

    public class SequenceMatcher<T> where T: IComparable<T>
    {
        public SequenceMatcher(List<T> a, List<T> b, bool autoJunk = true) : this(a, b, DefaultIsJunk, autoJunk)
        {
            // empty since the work is done in the other contstructor
        }

        public SequenceMatcher(List<T> a, List<T> b, Func<T, bool> isJunk, bool autoJunk = true)
        {
            this.a = a;
            this.b = b;
            this.isJunk = isJunk;
            this.autojunk = autoJunk;
            Chain_b();
        }

        List<T> a, b;
        HashSet<T> bJunk;
        Dictionary<T, List<int>> b2j = new Dictionary<T, List<int>>();
        bool autojunk = false;
        Func<T, bool> isJunk;

        public static bool DefaultIsJunk(T item)
        {
            return false;
        }

        private void Chain_b()
        {
            b2j = new Dictionary<T, List<int>>();

            var i = 0;
            foreach (var item in b)
            {
                if (!this.b2j.ContainsKey(item))
                {
                    b2j[item] = new List<int>();
                }
                b2j[item].Add(i);
                i++;
            }

            bJunk = new HashSet<T>();

            foreach (T c in b2j.Keys)
            {
                if (isJunk(c))
                {
                    bJunk.Add(c);
                }
            }
            foreach (T c in bJunk)
            {
                b2j.Remove(c);
            }

            if (autojunk && b.Count >= 200)
            {
                int ntest = b.Count / 100 + 1;
                var popular = b2j
                    .Where(kvp => kvp.Value.Count > ntest)
                    .Select(kvp => kvp.Key);
                               
                foreach (T item in popular)
                {
                    b2j.Remove(item);
                }
            }
        }

        public bool isBJunk(T item) => bJunk.Contains(item);

        public Match FindLongestMatch(int alo=0, int ahi = int.MaxValue, int blo = 0, int bhi = int.MaxValue)
        {
            if (ahi == int.MaxValue)
            {
                ahi = a.Count;
            }
            if (bhi == int.MaxValue)
            {
                bhi = b.Count;
            }

            var (besti, bestj, bestsize) = (alo, blo, 0);
            var j2len = new Dictionary<int, int>();
            var empty = new List<int>();
            for (int i = alo; i < ahi; i++)
            {
                var newj2len = new Dictionary<int, int>();
                // this seems inefficient
                foreach (int j in b2j.GetValueOrDefault(a[i], empty))
                {
                    if (j < blo) continue;
                    if (j >= bhi) break;
                    var k = newj2len[j] = j2len.GetValueOrDefault(j - 1, 0) + 1;
                    if (k > bestsize) {
                        (besti, bestj, bestsize) = (i - k + 1, j - k + 1, k);
                    }
                }
                j2len = newj2len;
            }

            // extend either direction by as many non-junk elements as possible.
            while (besti > alo && bestj > blo && !isBJunk(b[bestj-1]) 
                && a[besti-1].Equals(b[bestj-1]))
            {
                (besti, bestj, bestsize) = (besti - 1, bestj - 1, bestsize + 1);
            }

            while (besti+bestsize < ahi && bestj+bestsize < bhi && !isBJunk(b[bestj+bestsize])
                && a[besti+bestsize].Equals(b[bestj+bestsize]))
            {
                bestsize++;
            }

            while (besti > alo && bestj > blo && isBJunk(b[bestj-1]) 
                && a[besti-1].Equals(b[bestj-1]))
            {
                (besti, bestj, bestsize) = (besti - 1, bestj - 1, bestsize + 1);
            }

            while (besti + bestsize < ahi && bestj + bestsize < bhi && isBJunk(b[bestj + bestsize])
                && a[besti + bestsize].Equals(b[bestj + bestsize]))
            {
                bestsize++;
            }

            return new Match(besti, bestj, bestsize);
        }

        List<Match> matchingBlocks;
        public List<Match> GetMatchingBlocks()
        {
            if (matchingBlocks != null)
            {
                return matchingBlocks;
            }

            var queue = new Queue<Tuple<int, int, int, int>>();
            queue.Enqueue(Tuple.Create(0, a.Count, 0, b.Count));

            int alo, blo, ahi, bhi;
            int i, j, k;
            Match x;

            matchingBlocks = new List<Match>();
            while (queue.Count > 0)
            {
                (alo, ahi, blo, bhi) = queue.Dequeue();
                (i, j, k) = x = FindLongestMatch(alo, ahi, blo, bhi);
                if (k > 0)
                {
                    matchingBlocks.Add(x);
                    if (alo < i && blo < j)
                    {
                        queue.Enqueue(Tuple.Create(alo, i, blo, j));
                    }
                    if (i+k < ahi && j+k < bhi)
                    {
                        queue.Enqueue(Tuple.Create(i + k, ahi, j + k, bhi));
                    }
                }
            }
            matchingBlocks.Sort();

            int i1 = 0, j1 = 0, k1 = 0;
            var non_adjacent = new List<Tuple<int, int, int>>();
            int i2, j2, k2;
            foreach (Match m in matchingBlocks)
            {
                (i2, j2, k2) = m;
                if (i1 + k1 == i2 && j1 + k1 == j2)
                {
                    k1 += k2;
                }
                else
                {
                    if (k1 > 0)
                    {
                        non_adjacent.Add(Tuple.Create(i1, j1, k1));
                    }
                    (i1, j1, k1) = (i2, j2, k2);
                }
            }
            if (k1 > 0)
            {
                non_adjacent.Add(Tuple.Create(i1, j1, k1));
            }

            non_adjacent.Add(Tuple.Create(a.Count, b.Count, 0));

            matchingBlocks = non_adjacent.Select(tup => new Match(tup.Item1, tup.Item2, tup.Item3)).ToList();
            
            return matchingBlocks;
        }

        List<OpCode>? opcodes = null;
        public List<OpCode> GetOpCodes()
        {
            if (opcodes != null)
            {
                return opcodes;
            }
            int i = 0, j = 0;
            opcodes = new List<OpCode>();

            int ai, bj, size;
            foreach (var m in GetMatchingBlocks())
            {
                (ai, bj, size) = m;
                OpTags tag = OpTags.Empty;
                if (i < ai && j < bj)
                {
                    tag = OpTags.Replace;
                } else if (i < ai)
                {
                    tag = OpTags.Delete;
                } else if (j < bj)
                {
                    tag = OpTags.Insert;
                }
                if (tag != OpTags.Empty)
                {
                    opcodes.Add(new OpCode(tag, i, ai, j, bj));
                }
                (i, j) = (ai + size, bj + size);
                if (size > 0)
                {
                    opcodes.Add(new OpCode(OpTags.Equal, ai, i, bj, j));
                }
            }
            return opcodes;
        }

        List<List<OpCode>>? groupedOpCodes = null;
        public List<List<OpCode>> GetGroupedOpCodes(int n = 3)
        {
            if (groupedOpCodes != null)
            {
                return groupedOpCodes;
            }
            var codes = GetOpCodes();
            if (codes.Count == 0)
            {
                codes.Add(new OpCode(OpTags.Equal, 0, 1, 0, 1));
            }
            // Fixup leading and trailing groups if they show no changes.

            if (codes[0].tag == OpTags.Equal)
            {
                var code = codes[0];
                codes[0] = new OpCode(OpTags.Equal,
                    Math.Max(code.aBegin, code.aEnd - n), code.aEnd,
                    Math.Max(code.bBegin, code.bEnd - n), code.bEnd);
            }
            if (codes[codes.Count - 1].tag == OpTags.Equal)
            {
                var code = codes[-1];
                codes[codes.Count-1] = new OpCode(OpTags.Equal,
                    code.aBegin, Math.Min(code.aEnd, code.aBegin + n),
                    code.bBegin, Math.Min(code.bEnd, code.bBegin + n));
            }

            int nn = n + n;
            groupedOpCodes = new List<List<OpCode>>();
            var group = new List<OpCode>();
            OpTags tag;
            int i1, i2, j1, j2;
            foreach (var code in codes)
            {
                (tag, i1, i2, j1, j2) = code;
                // End the current group and start a new one whenever
                // there is a large range with no changes.
                if (code.tag == OpTags.Equal && code.aEnd - code.aBegin > nn)
                {
                    group.Add(new OpCode(tag,
                        i1, Math.Min(i2, i1 + n),
                        j1, Math.Min(j2, j1 + n)));
                    groupedOpCodes.Add(group);
                    group = new List<OpCode>();
                    (i1, j1) = (Math.Max(i1, i2-n), Math.Max(j1, j2 - n));

                }
                group.Add(new OpCode(tag, i1, i2, j1, j2));
            }
            if (group.Count > 0 && !(group.Count == 1 && group[0].tag == OpTags.Equal))
            {
                groupedOpCodes.Add(group);
            }
            return groupedOpCodes;
        }
    }
}