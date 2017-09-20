using Google.OrTools.ConstraintSolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hashi
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var hs = new HashiSolver();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Red is double and green is single bridge. 'H' and '|' are vertical. '-' and '=' are horizontal\n");
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine("p1:\n");
            hs.Solve(File.ReadAllText(@"p1.txt"));

            Console.Write("Press any key for next one.");
            Console.ReadKey();
            Console.Write("\r-----------------------------\n");
            Console.WriteLine("p2:\n");
            hs.Solve(File.ReadAllText(@"p2.txt"));
            Console.WriteLine("\n#END#");
            Console.ReadLine();
        }
    }

    public class HashiSolver
    {
        public HashiSolver()
        {
            Edge.ResetIndex();
        }

        protected class Node
        {
            public int Row, Col, Req;
            public bool Flag;

            public Node(int r, int c, int q)
            {
                Row = r;
                Col = c;
                Req = q;
            }
        }

        protected class Edge
        {
            private static int idx = 0;
            public Node A, B;
            public int Index;

            internal static void ResetIndex()
            {
                idx = 0;
            }

            public Edge(Node a, Node b)
            {
                A = a;
                B = b;
                Index = idx++;
            }
        }

        public int Solve(string input)
        {
            Edge.ResetIndex();

            IList<Node> nodes = Parse(input);
            var edges = new List<Edge>();
            //add edges between nodes;
            foreach (var node in nodes)
            {
                var rNode = nodes.Where(x => x.Row == node.Row && x.Col > node.Col).OrderBy(x => x.Col).FirstOrDefault();
                if (rNode != null)
                {
                    edges.Add(new Edge(node, rNode));
                }
                var dNode = nodes.Where(x => x.Col == node.Col && x.Row > node.Row).OrderBy(x => x.Row).FirstOrDefault();
                if (dNode != null)
                {
                    edges.Add(new Edge(node, dNode));
                }
            }
            using (var solver = new Solver("Hashi_" + Guid.NewGuid()))
            {
                int edgesNum = edges.Count;
                var toSolve = solver.MakeIntVarArray(edgesNum, 0, 2);
                //add total node edge total constraints
                foreach (var node in nodes)
                {
                    var node1 = node;
                    var toConsider = edges.Where(x => x.A == node1 || x.B == node1).Select(x => toSolve[x.Index]).ToArray();
                    solver.Add(solver.MakeSumEquality(toConsider, node.Req));
                }
                //add crossing edge constraints
                foreach (var ed in edges.Where(x => x.A.Row == x.B.Row))
                {
                    var e = ed;
                    var conflicts = edges.Where(x => x.A.Row < e.A.Row &&
                                                     x.B.Row > e.A.Row &&
                                                     x.A.Col > e.A.Col &&
                                                     x.A.Col < e.B.Col);
                    foreach (var conflict in conflicts)
                    {
                        solver.Add(solver.MakeEquality(toSolve[e.Index] * toSolve[conflict.Index], 0));
                    }
                }
                if (nodes.Count > 2)
                {
                    //remove 2=2 connections
                    foreach (var e in edges.Where(x => x.A.Req == 2 && x.B.Req == 2))
                    {
                        solver.Add(toSolve[e.Index] <= 1);
                    }
                    //remove 1-1 connections
                    foreach (var e in edges.Where(x => x.A.Req == 1 && x.B.Req == 1))
                    {
                        solver.Add(toSolve[e.Index] == 0);
                    }
                }
                var db = solver.MakePhase(toSolve, Solver.INT_VAR_DEFAULT, Solver.INT_VALUE_DEFAULT);
                solver.NewSearch(db);
                int c = 0;
                while (solver.NextSolution())
                {
                    if (AllConnected(toSolve, nodes, edges))
                    {
                        Print(toSolve, nodes, edges);
                        Console.WriteLine();
                        c++;
                    }
                }

                return c;
            }
        }

        private bool AllConnected(IntVar[] toSolve, IList<Node> nodes, List<Edge> edges)
        {
            var start = nodes[0];
            start.Flag = true;
            var s = new Stack<Node>();
            s.Push(start);
            while (s.Any())
            {
                var n = s.Pop();
                foreach (var edge in edges.Where(x => x.A == n || x.B == n))
                {
                    var o = edge.A == n ? edge.B : edge.A;
                    if (toSolve[edge.Index].Value() > 0 && !o.Flag)
                    {
                        o.Flag = true;
                        s.Push(o);
                    }
                }
            }
            bool r = nodes.All(x => x.Flag);
            foreach (var n in nodes)
            {
                n.Flag = false;
            }
            return r;
        }

        private void Print(IntVar[] toSolve, IList<Node> nodes, List<Edge> edges)
        {
            var l = new List<char[]>();
            for (int i = 0; i <= nodes.Max(x => x.Row); i++)
            {
                l.Add(new string(' ', nodes.Max(x => x.Col) + 1).ToCharArray());
            }
            foreach (var node in nodes)
            {
                l[node.Row][node.Col] = node.Req.ToString()[0];
                Node node1 = node;
                foreach (var edge in edges.Where(x => x.A == node1))
                {
                    var v = toSolve[edge.Index].Value();
                    if (v > 0)
                    {
                        //horizontal
                        if (edge.B.Row == node.Row)
                        {
                            char repl = v == 1 ? '-' : '=';
                            int col = node.Col + 1;
                            var r = l[node.Row];
                            while (col < edge.B.Col)
                            {
                                r[col] = repl;
                                col++;
                            }
                        }
                        //vertical
                        else
                        {
                            char repl = v == 1 ? '|' : 'H';
                            int row = node.Row + 1;
                            while (row < edge.B.Row)
                            {
                                l[row][node.Col] = repl;
                                row++;
                            }
                        }
                    }
                }
            }
            foreach (var r in l)
            {
                foreach (var c in r)
                {
                    if ('=' == c || 'H' == c)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if ('|' == c || '-' == c)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                    }

                    Console.Write(c);
                }
                Console.WriteLine();
            }
        }

        private IList<Node> Parse(string s)
        {
            var n = new List<Node>();
            int row = 0;
            foreach (var li in s.Split('\n'/*, StringSplitOptions.RemoveEmptyEntries*/))
            {
                string line = li.Trim('\r');
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] != ' ')
                    {
                        n.Add(new Node(row, col, line[col] - '0'));
                    }
                }
                row++;
            }
            return n;
        }
    }
}