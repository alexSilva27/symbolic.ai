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
    public class Constraints
    {
        [Fact]
        public void Test1()
        {
            // query X != 5 yields one solution with one assumption (one active constraint: X != 5).
            Variable x = new Variable();
            Number five = new Number(new Rational(5));
            Functor disunification = new Functor(Functor.UnequalFunctorName, new ATerm[]
            {
                x,
                five,
            });

            Program program = new Program(new Functor[] { }); // empty program.
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(program);

            int solutionsCount = 0;

            foreach (ExecutionEnvironment.SolutionAssumptions solutionAssumptions in executionEnvironment.ExecuteQuery(disunification))
            {
                solutionsCount++;

                Assert.True(solutionAssumptions.DelayedQueries.Count == 0);
                Assert.True(solutionAssumptions.ActiveConstraints.Count == 1);
                Assert.True(solutionAssumptions.ActiveConstraints[0].LeftTermValue == x);
                Assert.True(solutionAssumptions.ActiveConstraints[0].RightTermValue == five);
                Assert.True(solutionAssumptions.ActiveConstraints[0].ConstraintType == Constraint.Type.Disunification);
            }

            Assert.True(solutionsCount == 1);
        }

        [Fact]
        public void Test2()
        {
            // query "X != 5, X = 3" yields one solution with zero assumptions (once active constraint: X != 5 became X \= 3, which will always be true).
            Variable x = new Variable();
            Number five = new Number(new Rational(5));
            Functor disunification = new Functor(Functor.UnequalFunctorName, new ATerm[]
            {
                x,
                five,
            });

            Number three = new Number(new Rational(3));
            Functor unification = new Functor(Functor.EqualFunctorName, new ATerm[]
            {
                x,
                three,
            });

            Functor finalQuery = new Functor(Functor.ConjunctionFunctorName, new ATerm[]
            {
                disunification,
                unification,
            });

            Program program = new Program(new Functor[] { }); // empty program.
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(program);

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
            // query "X != 5, X = 5" yields zero solution.
            Variable x = new Variable();
            Number five = new Number(new Rational(5));
            Functor disunification = new Functor(Functor.UnequalFunctorName, new ATerm[]
            {
                x,
                five,
            });

            Functor unification = new Functor(Functor.EqualFunctorName, new ATerm[]
            {
                x,
                five,
            });

            Functor finalQuery = new Functor(Functor.ConjunctionFunctorName, new ATerm[]
            {
                disunification,
                unification,
            });

            Program program = new Program(new Functor[] { }); // empty program.
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(program);

            int solutionsCount = 0;

            foreach (ExecutionEnvironment.SolutionAssumptions solutionAssumptions in executionEnvironment.ExecuteQuery(finalQuery))
            {
                solutionsCount++;
            }

            Assert.True(solutionsCount == 0);
        }
    }
}