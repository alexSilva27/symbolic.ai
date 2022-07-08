//MIT License

//Copyright (c) 2022 - Alexandre Silva

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using InferenceEngine;

namespace InferenceEngineTests
{
    public class DirectedGraph
    {
        private const string _edgeFunctor = "edge";
        private const string _pathFunctor = "path";

        // graph containing 5 vertices: a, b, c, d and e.
        private Functor _a = new Functor("a", new ATerm[0]); // an atom is a functor with zero parameters.
        private Functor _b = new Functor("b", new ATerm[0]);
        private Functor _c = new Functor("c", new ATerm[0]);
        private Functor _d = new Functor("d", new ATerm[0]);
        private Functor _e = new Functor("e", new ATerm[0]);

        public Program BuildTestGraph()
        {
            // directed edges: a -> b -> c -> d -> e
            var edge1 = new Functor(_edgeFunctor, new ATerm[] { _a, _b });
            var edge2 = new Functor(_edgeFunctor, new ATerm[] { _b, _c });
            var edge3 = new Functor(_edgeFunctor, new ATerm[] { _c, _d });
            var edge4 = new Functor(_edgeFunctor, new ATerm[] { _d, _e });

            // path(A, B) :- edge(A, B).
            // path(A, B) :- edge(A, Aux), path(Aux, B).

            var variableA = new Variable();
            var variableB = new Variable();
            var variableAux = new Variable();

            var path1 = new Functor(Functor.HornClauseFunctorName, new ATerm[2]
            {
                new Functor(_pathFunctor, new ATerm[] { variableA, variableB }),
                new Functor(_edgeFunctor, new ATerm[] { variableA, variableB })
            });

            var path2 = new Functor(Functor.HornClauseFunctorName, new ATerm[2]
            {
                new Functor(_pathFunctor, new ATerm[] { variableA, variableB }),
                new Functor(Functor.ConjunctionFunctorName, new ATerm[]
                {
                    new Functor(_edgeFunctor, new ATerm[] { variableA, variableAux }),
                    new Functor(_pathFunctor, new ATerm[] { variableAux, variableB }),
                })
            });

            return new Program(new Functor[]
            {
                edge1,
                edge2,
                edge3,
                edge4,
                path1,
                path2,
            });
        }

        // Is there a connection from vertex a to vertex e? Yes.
        [Fact]
        public void Test1()
        {
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(BuildTestGraph());

            Functor pathBetweenAE = new Functor(_pathFunctor, new ATerm[] { _a, _e });

            // only one solution is expected.
            int solutionsCount = 0;

            foreach (var _ in executionEnvironment.ExecuteQuery(pathBetweenAE))
            {
                solutionsCount++;
            }

            Assert.True(solutionsCount == 1);
        }

        // Is there a connection from vertex e to vertex a? No.
        [Fact]
        public void Test2()
        {
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(BuildTestGraph());

            Functor pathBetweenEA = new Functor(_pathFunctor, new ATerm[] { _e, _a });

            int solutionsCount = 0;

            foreach (var _ in executionEnvironment.ExecuteQuery(pathBetweenEA))
            {
                solutionsCount++;
            }

            Assert.True(solutionsCount == 0);
        }

        // Get all vertices that are connected to A. 4 are expected: B, C, D and E.
        [Fact]
        public void Test3()
        {
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(BuildTestGraph());

            Variable result = new Variable();

            Functor pathsFromA = new Functor(_pathFunctor, new ATerm[] { _a, result });

            int solutionsCount = 0;

            foreach (var _ in executionEnvironment.ExecuteQuery(pathsFromA))
            {
                solutionsCount++;
            }

            Assert.True(solutionsCount == 4);
        }

        // Get all vertices that are connected to E. 0 are expected.
        [Fact]
        public void Test4()
        {
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(BuildTestGraph());

            Variable result = new Variable();

            Functor pathsFromE = new Functor(_pathFunctor, new ATerm[] { _e, result });

            int solutionsCount = 0;

            foreach (var _ in executionEnvironment.ExecuteQuery(pathsFromE))
            {
                solutionsCount++;
            }

            Assert.True(solutionsCount == 0);
        }
    }
}