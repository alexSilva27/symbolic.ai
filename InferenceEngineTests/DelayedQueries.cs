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
    public class DelayedQueries
    {
        string _functorMyProvableFact = "myProvableFact";

        [Fact]
        public void Test1()
        {
            // query "X" yields one solution with one assumption (one delayed query: X).
            Variable x = new Variable();

            Program program = new Program(new Functor[] { }); // empty program.
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(program);

            int solutionsCount = 0;

            foreach (ExecutionEnvironment.SolutionAssumptions solutionAssumptions in executionEnvironment.ExecuteQuery(x))
            {
                solutionsCount++;

                Assert.True(solutionAssumptions.DelayedQueries.Count == 1);
                Assert.True(solutionAssumptions.DelayedQueries[0].Value == x);

                Assert.True(solutionAssumptions.ActiveConstraints.Count == 0);
            }

            Assert.True(solutionsCount == 1);
        }

        [Fact]
        public void Test2()
        {
            // query "X, X = myProvableFact()" yields one solution with zero assumption (once delayed query: X was transformed into an actual query: myProvableFact() and then called).
            Variable x = new Variable();

            Program program = new Program(new Functor[] { new Functor(_functorMyProvableFact, new ATerm[0]) }); // program containing one fact: myProvableFact().
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(program);

            var finalQuery = new Functor(Functor.ConjunctionFunctorName, new ATerm[]
            {
                x,
                new Functor(Functor.EqualFunctorName, new ATerm[]
                {
                    x,
                    new Functor(_functorMyProvableFact, new ATerm[0]),
                })
            });

            int solutionsCount = 0;

            foreach (ExecutionEnvironment.SolutionAssumptions solutionAssumptions in executionEnvironment.ExecuteQuery(finalQuery))
            {
                solutionsCount++;

                Assert.True(solutionAssumptions.DelayedQueries.Count == 0);
                Assert.True(solutionAssumptions.ActiveConstraints.Count == 0);
            }

            Assert.True(solutionsCount == 1);
        }

        [Fact]
        public void Test3()
        {
            // query "X, X = myUnprovableFact()" yields zero solution since myUnprovableFact() cannot be proved (yields zero solution).
            Variable x = new Variable();

            Program program = new Program(new Functor[] { new Functor(_functorMyProvableFact, new ATerm[0]) }); // program containing one fact: myProvableFact().
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(program);

            var finalQuery = new Functor(Functor.ConjunctionFunctorName, new ATerm[]
            {
                x,
                new Functor(Functor.EqualFunctorName, new ATerm[]
                {
                    x,
                    new Functor("myUnprovableFact", new ATerm[0]),
                })
            });

            int solutionsCount = 0;

            foreach (ExecutionEnvironment.SolutionAssumptions solutionAssumptions in executionEnvironment.ExecuteQuery(finalQuery))
            {
                solutionsCount++;
            }

            Assert.True(solutionsCount == 0);
        }
    }
}