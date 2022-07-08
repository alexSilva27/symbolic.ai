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

namespace InferenceEngine
{
    public class Program
    {
        // a fact is simply a horn clause whose body is true.
        public readonly IReadOnlyList<Functor> HornClausesAndFacts;

        public Program(IReadOnlyList<Functor> hornClausesAndFacts)
        {
            foreach (var hornClauseOrFact in hornClausesAndFacts)
            {
                if (hornClauseOrFact.Name == Functor.HornClauseFunctorName && hornClauseOrFact.Arity != 2)
                {
                    throw new Exception("Functor horn clause is expected to have Arity = 2");
                }
                else if (hornClauseOrFact.IsBuiltInFunctor && hornClauseOrFact.Name != Functor.HornClauseFunctorName)
                {
                    throw new Exception(String.Format("Built-in functor {0} cannot be used as a fact or horn clause", hornClauseOrFact.Name));
                }
            }

            HornClausesAndFacts = hornClausesAndFacts;
        }
    }
}