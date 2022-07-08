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
    public class Functor : ATerm, IDisposable
    {
        #region Public API

        public readonly string Name;
        public readonly IReadOnlyList<ATerm> Parameters;

        public const string DisjunctionFunctorName = ";";
        public const string ConjunctionFunctorName = ",";
        public const string EqualFunctorName = "=";
        public const string UnequalFunctorName = "!=";
        public const string GreaterFunctorName = ">";
        public const string GreaterEqualFunctorName = ">=";
        public const string SmallerFunctorName = "<";
        public const string SmallerEqualFunctorName = "<=";
        public const string NegationAsFailureFunctorName = "not";
        public const string HornClauseFunctorName = ":-";
        public const string AddFunctorName = "+";
        public const string SubstractFunctorName = "-";
        public const string MultiplyFunctorName = "*";
        public const string DivideFunctorName = "/";

        internal static string[] _builtInFunctors = new string[]
        {
            DisjunctionFunctorName, ConjunctionFunctorName, EqualFunctorName, UnequalFunctorName, GreaterFunctorName, GreaterEqualFunctorName,
            SmallerFunctorName, SmallerEqualFunctorName, NegationAsFailureFunctorName, HornClauseFunctorName, AddFunctorName, SubstractFunctorName,
            MultiplyFunctorName, DivideFunctorName
        };

        internal bool IsBuiltInFunctor => _builtInFunctors.Contains(Name);

        internal bool IsArithmeticExpression => Arity == 2 && 
            (Name == AddFunctorName || Name == SubstractFunctorName || Name == MultiplyFunctorName || Name == DivideFunctorName);

        public int Arity => Parameters.Count;

        internal bool IsInCallStack;
        internal bool MaxQueryCallStackCountReached; // during this query execution, the max query call stack was reached?

        public Functor(string name, IReadOnlyList<ATerm> parameters)
        {
            Name = name;
            Parameters = parameters;

            foreach (var parameter in Parameters)
            {
                parameter.OccursAsParameterInFunctors.Add(this);
            }
        }

        internal void OnMaxQueryCallStackCountReached()
        {
            MaxQueryCallStackCountReached = true;
        }

        public void Dispose()
        {
            foreach (var parameter in Parameters)
            {
                parameter.OccursAsParameterInFunctors.Remove(this);
            }
        }

        // unassignedVariables.Value is how many times each variable is referred to.
        internal override void GetUnassignedVariables(Dictionary<Variable, int> unassignedVariables) // variables whose .Next == null.
        {
            foreach (var parameter in Parameters)
            {
                parameter.Dereferenced.GetUnassignedVariables(unassignedVariables);
            }
        }

        #endregion
    }
}