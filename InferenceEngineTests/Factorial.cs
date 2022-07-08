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
    public class Factorial
    {
        private const string _factorialName = "factorial";

        // n_factorial(0, 1).
        //
        // n_factorial(N, F) :-
        //     N >= 1,
        //     N1 = N - 1,
        //     F #= N * F1,
        //     n_factorial(N1, F1).
        public Program BuildFactorialProgram()
        {
            Number zero = new Number(Rational.Zero);
            Number one = new Number(Rational.One);
            Variable n = new Variable();
            Variable n1 = new Variable();
            Variable f = new Variable();
            Variable f1 = new Variable();

            Functor greaterEqual = new Functor(Functor.GreaterEqualFunctorName, new ATerm[] { n, one });
            Functor equality1 = new Functor(Functor.EqualFunctorName, new ATerm[] { n1, new Functor(Functor.SubstractFunctorName, new ATerm[] { n, one }) });
            Functor equality2 = new Functor(Functor.EqualFunctorName, new ATerm[] { f, new Functor(Functor.MultiplyFunctorName, new ATerm[] { n, f1 }) });

            Functor factorialBody =
            new Functor(Functor.ConjunctionFunctorName, new ATerm[]
            {
                greaterEqual,
                new Functor(Functor.ConjunctionFunctorName, new ATerm[]
                {
                    equality1,
                    new Functor(Functor.ConjunctionFunctorName, new ATerm[]
                    {
                        equality2,
                        new Functor(_factorialName, new ATerm[] { n1, f1 })
                    })
                })
            });

            Functor factorialFact = new Functor(_factorialName, new ATerm[] { zero, one });
            Functor factorialHead = new Functor(_factorialName, new ATerm[] { n, f });
            Functor factorialHornClause = new Functor(Functor.HornClauseFunctorName, new ATerm[] { factorialHead, factorialBody });

            return new Program(new Functor[] { factorialFact, factorialHornClause });
        }

        private long FactorialTest(long n) => n > 0 ? n * FactorialTest(n - 1) : 1;

        /// <summary>
        /// Retrieve the first 10 "inputs" and "outputs" that make the factorial relation true.
        /// </summary>
        [Fact]
        public void Test1()
        {
            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(BuildFactorialProgram());

            var input = new Variable();
            var result = new Variable();

            long i = 0;

            foreach (var _ in executionEnvironment.ExecuteQuery(new Functor(_factorialName, new ATerm[] { input, result })))
            {
                Rational expectedFactorialInput = new Rational(i);
                Rational expectedFactorialResult = new Rational(FactorialTest(i));

                Rational factorialInput = (input.Value as Number)!.Value;
                Rational factorialResult = (result.Value as Number)!.Value;

                Assert.True(factorialInput == expectedFactorialInput);
                Assert.True(factorialResult == expectedFactorialResult);

                if (++i >= 10) break; 
            }
        }

        // What is the factorial of 12?
        [Fact]
        public void Test2()
        {
            // Only one solution is expected.

            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(BuildFactorialProgram());

            var input = new Number(new Rational(12));
            var result = new Variable();

            long solutionsCount = 0;

            foreach (var _ in executionEnvironment.ExecuteQuery(new Functor(_factorialName, new ATerm[] { input, result })))
            {
                Rational expectedFactorialResult = new Rational(FactorialTest(12));
                
                Rational factorialResult = (result.Value as Number)!.Value;
                Assert.True(factorialResult == expectedFactorialResult);

                solutionsCount++;
            }

            Assert.True(solutionsCount == 1);
        }

        // Which input gives 479,001,600 as factorial?
        [Fact]
        public void Test3()
        {
            // Only one solution is expected.

            ExecutionEnvironment executionEnvironment = new ExecutionEnvironment(BuildFactorialProgram());

            var input = new Variable();
            var result = new Number(new Rational(479001600));

            foreach (var _ in executionEnvironment.ExecuteQuery(new Functor(_factorialName, new ATerm[] { input, result })))
            {
                Rational expectedFactorialInput = new Rational(12);
                Assert.True(FactorialTest(12) == 479001600);

                Rational factorialInput = (input.Value as Number)!.Value;
                Assert.True(factorialInput == expectedFactorialInput);

                // no need, to look indefinitly for other solutions.
                break;
            }
        }
    }
}