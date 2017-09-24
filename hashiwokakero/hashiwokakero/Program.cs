using Google.OrTools.ConstraintSolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hashi
{
    // using google optimization tools
    // https://developers.google.com/optimization/
    // for clarification:
    // https://developers.google.com/optimization/cp/cp_solver

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
            // reset static variable
            Edge.ResetIndex();
        }

        // for storing node data
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

        // bridge
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
            // just in case
            Edge.ResetIndex();

            // get all nodes
            IList<Node> nodes = Parse(input);
            var edges = new List<Edge>();

            //add edges between nodes;
            foreach (var node in nodes)
            {
                // right node of the current one
                var rNode = nodes.Where(x => x.Row == node.Row && x.Col > node.Col).OrderBy(x => x.Col).FirstOrDefault();
                if (rNode != null)
                {
                    // add edge between two
                    edges.Add(new Edge(node, rNode));
                }

                // down node of the current one
                var dNode = nodes.Where(x => x.Col == node.Col && x.Row > node.Row).OrderBy(x => x.Row).FirstOrDefault();
                if (dNode != null)
                {
                    // add edge between two
                    edges.Add(new Edge(node, dNode));
                }
            }

            // using google or tools solver to solve it
            using (var solver = new Solver("Hashi_" + Guid.NewGuid()))
            {
                int edgesNum = edges.Count;
                // constraint to 0, 1 or 2 bridge for each node pair
                // using solver array - list of edgesNum count of ints where each is 0, 1, or 2 - represents bridges
                var toSolve = solver.MakeIntVarArray(edgesNum, 0, 2);
                //add total node edge total constraints
                foreach (var node in nodes)
                {
                    var node1 = node;
                    // what edges to consider for curent node
                    var toConsider = edges.Where(x => x.A == node1 || x.B == node1).Select(x => toSolve[x.Index]).ToArray();
                    // sum of edges from current node must be equals to required (Req) number
                    solver.Add(solver.MakeSumEquality(toConsider, node.Req));
                }
                //add crossing edge constraints
                foreach (var ed in edges.Where(x => x.A.Row == x.B.Row))
                {
                    var e = ed;
                    // there are no diagonal bridges
                    var conflicts = edges.Where(x => x.A.Row < e.A.Row &&
                                                     x.B.Row > e.A.Row &&
                                                     x.A.Col > e.A.Col &&
                                                     x.A.Col < e.B.Col);
                    foreach (var conflict in conflicts)
                    {
                        // for each diagonal nodes, sum of edges must be 0 - no bridge between
                        solver.Add(solver.MakeEquality(toSolve[e.Index] * toSolve[conflict.Index], 0));
                    }
                }

                // if there are mroe than 2 nodes we must remove possible "islands"
                if (nodes.Count > 2)
                {
                    //remove 2=2 connections - island
                    foreach (var e in edges.Where(x => x.A.Req == 2 && x.B.Req == 2))
                    {
                        solver.Add(toSolve[e.Index] <= 1);
                    }
                    //remove 1-1 connections - island
                    foreach (var e in edges.Where(x => x.A.Req == 1 && x.B.Req == 1))
                    {
                        solver.Add(toSolve[e.Index] == 0);
                    }
                }
                // making solver for the problem just defined
                // toSolve - using array of edges
                // INT_VAR_DEFAULT - how to assign variables - default is in order in the vector (array)
                // INT_VALUE_DEFAULT - how to assign values - default is in incremental order
                var db = solver.MakePhase(toSolve, Solver.INT_VAR_DEFAULT, Solver.INT_VALUE_DEFAULT);
                // make solutions
                solver.NewSearch(db);
                int solutionsCount = 0;
                // go through solutions
                while (solver.NextSolution())
                {
                    // check that all are connected, no islands
                    if (AllConnected(toSolve, nodes, edges))
                    {
                        // if everything connected, print that solution
                        Print(toSolve, nodes, edges);
                        Console.WriteLine();
                        solutionsCount++;
                    }
                }
                // return number of solutions
                return solutionsCount;
            }
        }

        /// <summary>
        /// checks if all nodes in grid are connected in one "graph"
        /// </summary>
        /// <param name="toSolve"></param>
        /// <param name="nodes"></param>
        /// <param name="edges"></param>
        /// <returns></returns>
        private bool AllConnected(IntVar[] toSolve, IList<Node> nodes, List<Edge> edges)
        {
            var start = nodes[0];
            // first one is connected by definition (it is alone)
            start.Flag = true;
            var s = new Stack<Node>();
            s.Push(start);
            // while there are nodes that are connected
            while (s.Any())
            {
                // remove the connected one from the stack
                var n = s.Pop();
                // go through edges that could connect current node n with something else
                foreach (var edge in edges.Where(x => x.A == n || x.B == n))
                {
                    var otherNode = edge.A == n ? edge.B : edge.A;
                    // if other node is connected and not flagged as counted,
                    // flag it and push it to the stack for further analysis
                    if (toSolve[edge.Index].Value() > 0 && !otherNode.Flag)
                    {
                        otherNode.Flag = true;
                        s.Push(otherNode);
                    }
                }
            }
            // if all the nodes are flagged everything is connected, otherwise not
            bool r = nodes.All(x => x.Flag);
            // remove flags and return result
            foreach (var n in nodes)
            {
                n.Flag = false;
            }
            return r;
        }

        /// <summary>
        /// prints solution
        /// </summary>
        /// <param name="toSolve"></param>
        /// <param name="nodes"></param>
        /// <param name="edges"></param>
        private void Print(IntVar[] toSolve, IList<Node> nodes, List<Edge> edges)
        {
            var lines = new List<char[]>();
            for (int i = 0; i <= nodes.Max(x => x.Row); i++)
            {
                lines.Add(new string(' ', nodes.Max(x => x.Col) + 1).ToCharArray());
            }
            foreach (var node in nodes)
            {
                lines[node.Row][node.Col] = node.Req.ToString()[0];
                Node node1 = node;
                foreach (var edge in edges.Where(x => x.A == node1))
                {
                    var v = toSolve[edge.Index].Value();
                    if (v > 0)
                    {
                        //horizontal bridges
                        if (edge.B.Row == node.Row)
                        {
                            // it is 1 or 2 bridges definitely
                            char repl = v == 1 ? '-' : '=';
                            int col = node.Col + 1;
                            var r = lines[node.Row];
                            while (col < edge.B.Col)
                            {
                                r[col] = repl;
                                col++;
                            }
                        }
                        //vertical bridges
                        else
                        {
                            // it is 1 or 2 bridges definitely
                            char repl = v == 1 ? '|' : 'H';
                            int row = node.Row + 1;
                            while (row < edge.B.Row)
                            {
                                lines[row][node.Col] = repl;
                                row++;
                            }
                        }
                    }
                }
            }

            // print constructed "lines" (rows) with bridges
            foreach (char[] row in lines)
            {
                foreach (var c in row)
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

        /// <summary>
        /// parse string input of the grid that needs to be solved
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
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