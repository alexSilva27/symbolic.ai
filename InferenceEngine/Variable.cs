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
    public class Variable : ATerm
    {
        #region Private instance & static fields

        private static int _nextAvailableID;
        private readonly int _id;
        private bool _isDelayedQuery;

        #endregion

        #region Public API

        internal bool IsDelayedQuery
        {
            get
            {
                if (_isDelayedQuery)
                {
                    return true;
                }
                else
                {
                    foreach (Variable previousVariable in Previous)
                    {
                        if (previousVariable.IsDelayedQuery)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            set
            {
                _isDelayedQuery = value;
            }
        }

        public ATerm Value => Dereferenced;

        internal override ATerm Dereferenced => Next is null ? this : Next.Dereferenced;

        internal ATerm? Next;

        internal readonly HashSet<Constraint> Constraints = new();

        public Variable()
        {
            _id = _nextAvailableID++;
        }

        internal bool IsOlderThan(Variable otherVariable) => _id < otherVariable._id;

        // Next == null.
        internal bool DoesOccurInFunctor(Functor functor)
        {
            foreach (var parameter in functor.Parameters)
            {
                var parameterDereferenced = parameter.Dereferenced;
                if (this == parameterDereferenced || (parameterDereferenced is Functor parameterAsFunctor && DoesOccurInFunctor(parameterAsFunctor)))
                {
                    return true;
                }
            }

            return false;
        }

        internal override void GetUnassignedVariables(Dictionary<Variable, int> unassignedVariables)
        {
            if (Next == null)
            {
                unassignedVariables.TryGetValue(this, out int count);
                unassignedVariables[this] = ++count;
            }
        }

        internal override void GetConstraints(HashSet<Constraint> constraints)
        {
            base.GetConstraints(constraints);
            constraints.UnionWith(Constraints.Where(x => !x.Discarded));
        }

        #endregion
    }
}