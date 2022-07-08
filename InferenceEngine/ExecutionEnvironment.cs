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

using System.Collections;

namespace InferenceEngine
{
    public class ExecutionEnvironment
    {
        #region Private fields & properties

        private ExecutionState CurrentExecutionState => new(_variablesAssigned.Count, _delayedQueries.Count, _queriesCallStack.Count,
            _constraints.Count, _constraintsDiscarded.Count, _instantiatedFunctors.Count, _solutionID.Count);

        private readonly Stack<Variable> _variablesAssigned = new();
        private readonly Stack<Variable> _delayedQueries = new();
        private readonly Stack<Functor> _queriesCallStack = new();
        private readonly Stack<Constraint> _constraints = new();
        private readonly Stack<Constraint> _constraintsDiscarded = new();
        private readonly Stack<Functor> _instantiatedFunctors = new();
        private readonly Stack<int> _solutionID = new(); // the whole sequence of ints represent a specific solution (specific inference path).

        private event Action? MaxQueryCallStackCountReached;

        private const int _initialMaxQueryCallStackCount = 50;
        private const int _maxQueryCallStackCountMultiplier = 2;
        private const int _maxQueryCallStackCount = 2000;

        #endregion

        #region Public API

        // for example, executing the query: X != Y succeeds (yielding one solution) with the engine returning X != Y as one active constraint.
        // On the contrary, query: X != Y, X = 5, Y = 6 succeeds (yielding also one solution) but with no active constraint (5 != 6 will always be true). 
        public readonly struct SolutionAssumptions
        {
            /// <summary>
            /// The delayed queries that will have to have solution in order for the yielded solution to be valid.
            /// </summary>
            public readonly IReadOnlyList<Variable> DelayedQueries;

            /// <summary>
            /// The constraints that are still active at the time a solution was yielded. 
            /// </summary>
            public readonly IReadOnlyList<Constraint> ActiveConstraints;

            public SolutionAssumptions(IReadOnlyList<Variable> delayedQueries, IReadOnlyList<Constraint> activeConstraints)
            {
                DelayedQueries = delayedQueries;
                ActiveConstraints = activeConstraints;
            }
        }

        public Program CurrentProgram { get; set; }

        public ExecutionEnvironment(Program program)
        {
            CurrentProgram = program;
        }

        public IEnumerable<SolutionAssumptions> ExecuteQuery(ATerm query)
        {
            int currentMaxQueryCallStackCount = _initialMaxQueryCallStackCount;
            bool continueSearch = true;

            void continueSetter() => continueSearch = true;
            MaxQueryCallStackCountReached += continueSetter;

            List<int[]> solutionsIDAlreadyReturned = new();

            while (continueSearch)
            {
                continueSearch = false;

                foreach (var _ in ExecuteQuery(query, currentMaxQueryCallStackCount))
                {
                    int[] newSolutionID = _solutionID.ToArray();

                    // check if this solution was already returned.
                    bool previousSolutionIDFound = false;

                    foreach (var previousSolutionID in solutionsIDAlreadyReturned)
                    {
                        if (previousSolutionID.Length == newSolutionID.Length)
                        {
                            previousSolutionIDFound = true;

                            for (int i = 0; i < previousSolutionID.Length; i++)
                            {
                                if (newSolutionID[i] != previousSolutionID[i])
                                {
                                    previousSolutionIDFound = false;
                                    break;
                                }
                            }
                        }

                        if (previousSolutionIDFound) break;
                    }

                    if (!previousSolutionIDFound)
                    {
                        solutionsIDAlreadyReturned.Add(newSolutionID);

                        var delayedQueries = new List<Variable>();

                        foreach (var delayedQuery in _delayedQueries)
                        {
                            if (delayedQuery.Dereferenced is Variable delayedQueryDereferenced)
                            {
                                delayedQueries.Add(delayedQueryDereferenced);
                            }
                        }

                        var activeConstraints = _constraints.Where(x => !x.Discarded).ToList();
                        
                        yield return new SolutionAssumptions(delayedQueries, activeConstraints);
                    }
                }

                if (currentMaxQueryCallStackCount == _maxQueryCallStackCount)
                {
                    continueSearch = false;
                }
                else
                {
                    currentMaxQueryCallStackCount = Math.Min(_maxQueryCallStackCount, currentMaxQueryCallStackCount * _maxQueryCallStackCountMultiplier);
                }
            }

            MaxQueryCallStackCountReached -= continueSetter;
        }

        #endregion

        #region Term instantiation

        private Functor InstantiateFunctor(Functor functor, Dictionary<Variable, Variable> compileToRuntimeVariable)
        {
            var instantiatedFunctorParameters = new ATerm[functor.Arity];

            for (int i = 0; i < functor.Arity; i++)
            {
                instantiatedFunctorParameters[i] = functor.Parameters[i] switch
                {
                    Functor parameterAsFunctor => InstantiateFunctor(parameterAsFunctor, compileToRuntimeVariable),
                    Variable parameterAsVariable => InstantiateVariable(parameterAsVariable, compileToRuntimeVariable),
                    Number number => new Number(number.Value),
                    _ => throw new NotImplementedException(),
                };
            }

            return _instantiatedFunctors.Push(new Functor(functor.Name, instantiatedFunctorParameters));
        }

        private static Variable InstantiateVariable(Variable variable, Dictionary<Variable, Variable> compileToRuntimeVariable)
        {
            if (!compileToRuntimeVariable.TryGetValue(variable, out var runtimeVariable))
            {
                runtimeVariable = new Variable();
                compileToRuntimeVariable[variable] = runtimeVariable;
            }
            return runtimeVariable;
        }

        #endregion

        #region Unification and constraints

        private bool Unify(ATerm dereferencedLeftTerm, ATerm dereferencedRightTerm)

            => (dereferencedLeftTerm, dereferencedRightTerm) switch
            {
                (Functor leftFunctor, Functor rightFunctor) => Unify(leftFunctor, rightFunctor),
                (Variable leftVariable, _) => Unify(leftVariable, dereferencedRightTerm),
                (_, Variable rightVariable) => Unify(rightVariable, dereferencedLeftTerm),
                (Number leftNumber, Number rightNumber) => leftNumber.Value == rightNumber.Value,
                _ => false,
            };

        private bool Unify(Functor functor, Functor otherFunctor)
        {
            if (functor.Name == otherFunctor.Name && functor.Arity == otherFunctor.Arity)
            {
                var stateBeforeUnification = CurrentExecutionState;

                for (int i = 0; i < functor.Arity; i++)
                {
                    if (!Unify(functor.Parameters[i].Dereferenced, otherFunctor.Parameters[i].Dereferenced))
                    {
                        RestoreExecutionState(stateBeforeUnification);
                        return false;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private bool Unify(Variable dereferencedVariable, ATerm dereferencedTerm)
        {
            switch (dereferencedTerm)
            {
                case Functor functor when dereferencedVariable.DoesOccurInFunctor(functor):
                    {
                        return false;
                    }
                case Variable variable when variable == dereferencedVariable: // X = X.
                    {
                        return true;
                    }
                case Variable variable when dereferencedVariable.IsOlderThan(variable):
                    {
                        variable.Next = dereferencedVariable;
                        dereferencedVariable.Previous.Add(variable);
                        _variablesAssigned.Push(variable);
                        return true;
                    }
                default:
                    {
                        dereferencedVariable.Next = dereferencedTerm;
                        dereferencedTerm.Previous.Add(dereferencedVariable);
                        _variablesAssigned.Push(dereferencedVariable);
                        return true;
                    }
            }
        }

        #endregion

        #region Query execution

        private readonly struct ExecutionState
        {
            public readonly int VariablesAssignedCount;
            public readonly int DelayedQueriesCount;
            public readonly int QueriesCallStackCount;
            public readonly int ConstraintsCount;
            public readonly int ConstraintsDiscardedCount;
            public readonly int InstantiatedFunctorsCount;
            public readonly int SolutionIDCount;

            public ExecutionState(int variablesAssignedCount, int delayedQueriesCount, int queriesCallStackCount,
                                  int constraintsCount, int constraintsDiscardedCount, int instantiatedFunctorsCount, int solutionIDCount)
            {
                VariablesAssignedCount = variablesAssignedCount;
                DelayedQueriesCount = delayedQueriesCount;
                QueriesCallStackCount = queriesCallStackCount;
                ConstraintsCount = constraintsCount;
                ConstraintsDiscardedCount = constraintsDiscardedCount;
                InstantiatedFunctorsCount = instantiatedFunctorsCount;
                SolutionIDCount = solutionIDCount;
            }
        }

        // return the execution state that allows undoing the registration to be done.
        private ExecutionState RegisterQueryExecution(Functor query, bool notifyOnMaxQueryCallStackCountReached = false, int? choicePointID = null)
        {
            var oldState = CurrentExecutionState;

            query.IsInCallStack = true;
            _queriesCallStack.Push(query);

            if (notifyOnMaxQueryCallStackCountReached)
            {
                MaxQueryCallStackCountReached += query.OnMaxQueryCallStackCountReached;
            }

            if (choicePointID != null)
            {
                _solutionID.Push(choicePointID.Value);
            }

            return oldState;
        }

        // return the execution state that allows undoing the registration to be done.
        private ExecutionState RegisterDelayedQuery(Variable delayedQuery)
        {
            var oldState = CurrentExecutionState;

            delayedQuery.IsDelayedQuery = true;
            _delayedQueries.Push(delayedQuery);

            return oldState;
        }

        private void RestoreExecutionState(ExecutionState stateToBeRestored)
        {
            while (_variablesAssigned.Count != stateToBeRestored.VariablesAssignedCount)
            {
                Variable variable = _variablesAssigned.Pop();
                variable.Next!.Previous.Remove(variable);
                variable.Next = null;
            }

            while (_delayedQueries.Count != stateToBeRestored.DelayedQueriesCount)
            {
                _delayedQueries.Pop().IsDelayedQuery = false;
            }

            while (_queriesCallStack.Count != stateToBeRestored.QueriesCallStackCount)
            {
                var query = _queriesCallStack.Pop();
                query.IsInCallStack = false;
                query.MaxQueryCallStackCountReached = false;
                MaxQueryCallStackCountReached -= query.OnMaxQueryCallStackCountReached;
            }

            while (_constraintsDiscarded.Count != stateToBeRestored.ConstraintsDiscardedCount)
            {
                _constraintsDiscarded.Pop().Discarded = false;
            }

            while (_constraints.Count != stateToBeRestored.ConstraintsCount)
            {
                var constraint = _constraints.Pop();

                foreach (var variable in constraint.UnassignedVariables)
                {
                    variable.Key.Constraints.Remove(constraint);
                }
            }

            while (_instantiatedFunctors.Count != stateToBeRestored.InstantiatedFunctorsCount)
            {
                _instantiatedFunctors.Pop().Dispose();
            }

            while (_solutionID.Count != stateToBeRestored.SolutionIDCount)
            {
                _solutionID.Pop();
            }
        }

        private IEnumerable ExecuteQuery(ATerm dereferencedQuery, int maxQueryCallStackCountAllowed)

            => dereferencedQuery switch
            {
                Number number
                    => ExecuteNumberQuery(number),

                Variable variable
                    => ExecuteDelayedQuery(variable),

                Functor functor when functor.IsInCallStack
                    => ExecuteQueryThatIsAlreadyExecuting(functor),

                Functor functor when maxQueryCallStackCountAllowed == _queriesCallStack.Count
                    => ExecuteQueryWhenMaxQueryCallStackCountReached(functor),

                Functor functor when functor.Name == Functor.NegationAsFailureFunctorName && functor.Arity == 1
                    => ExecuteNegationAsFailure(functor, maxQueryCallStackCountAllowed),

                Functor functor when functor.Name == Functor.ConjunctionFunctorName && functor.Arity == 2
                    => ExecuteLogicalConjunction(functor, maxQueryCallStackCountAllowed),

                Functor functor when functor.Name == Functor.DisjunctionFunctorName && functor.Arity == 2
                    => ExecuteLogicalDisjunction(functor, maxQueryCallStackCountAllowed),

                Functor functor when functor.Name == Functor.GreaterEqualFunctorName && functor.Arity == 2
                    => ExecuteArithmeticGreaterEqualOrSmallerEqual(functor, maxQueryCallStackCountAllowed, true),

                Functor functor when functor.Name == Functor.SmallerEqualFunctorName && functor.Arity == 2
                    => ExecuteArithmeticGreaterEqualOrSmallerEqual(functor, maxQueryCallStackCountAllowed, false),

                Functor functor when functor.Name == Functor.EqualFunctorName && functor.Arity == 2
                    => ExecuteUnificationOrArithmeticEquality(functor, maxQueryCallStackCountAllowed),

                Functor functor when (functor.Name == Functor.UnequalFunctorName || functor.Name == Functor.GreaterFunctorName || functor.Name == Functor.SmallerFunctorName) 
                                     && functor.Arity == 2
                    => ExecuteDisunificationOrArithmeticNonEquality(functor), 

                Functor functor
                    => ExecuteInverseDeduction(functor, maxQueryCallStackCountAllowed),
                
                _ => throw new NotImplementedException(),
            };

        private IEnumerable ExecuteNumberQuery(Number _)
        {
            yield return default; // a number is proven to exist, by definition.
        }

        private IEnumerable ExecuteDelayedQuery(Variable dereferencedVariable)
        {
            if (dereferencedVariable.IsDelayedQuery)
            {
                yield return default;
            }
            else
            {
                var stateBeforeRegistration = RegisterDelayedQuery(dereferencedVariable);
                yield return default;
                RestoreExecutionState(stateBeforeRegistration);
            }
        }

        private IEnumerable ExecuteQueryThatIsAlreadyExecuting(Functor _)
        {
            yield return default;
        }

        private IEnumerable ExecuteQueryWhenMaxQueryCallStackCountReached(Functor _)
        {
            MaxQueryCallStackCountReached?.Invoke();
            yield break;
        }

        private IEnumerable ExecuteNegationAsFailure(Functor negationAsFailure, int maxQueryCallStackCountAllowed)
        {
            var stateBeforeRegistration = RegisterQueryExecution(negationAsFailure, true);

            // we can only prove that the functor parameter has no solution if no solution was found according to the current max allowed depth
            // and the current max allowed depth was not reached at any moment during the search for solutions for that functor parameter.
            if (!ExecuteQuery(negationAsFailure.Parameters[0].Dereferenced, maxQueryCallStackCountAllowed).GetEnumerator().MoveNext()
                && !negationAsFailure.MaxQueryCallStackCountReached)
            {
                MaxQueryCallStackCountReached -= negationAsFailure.OnMaxQueryCallStackCountReached; // dont need to wait for RestoreExecutionState().
                yield return default;
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        private IEnumerable ExecuteLogicalConjunction(Functor logicalConjunction, int maxQueryCallStackCountAllowed)
        {
            var stateBeforeRegistration = RegisterQueryExecution(logicalConjunction);

            foreach (var _ in ExecuteQuery(logicalConjunction.Parameters[0].Dereferenced, maxQueryCallStackCountAllowed))
            {
                foreach (var __ in ExecuteQuery(logicalConjunction.Parameters[1].Dereferenced, maxQueryCallStackCountAllowed))
                {
                    yield return default;
                }
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        private IEnumerable ExecuteLogicalDisjunction(Functor logicalDisjunction, int maxQueryCallStackCountAllowed)
        {
            var stateBeforeRegistration = RegisterQueryExecution(logicalDisjunction, false, -1);

            foreach (var _ in ExecuteQuery(logicalDisjunction.Parameters[0].Dereferenced, maxQueryCallStackCountAllowed))
            {
                yield return default;
            }

            RestoreExecutionState(stateBeforeRegistration);
            stateBeforeRegistration = RegisterQueryExecution(logicalDisjunction, false, -2);

            foreach (var _ in ExecuteQuery(logicalDisjunction.Parameters[1].Dereferenced, maxQueryCallStackCountAllowed))
            {
                yield return default;
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        private IEnumerable ExecuteArithmeticGreaterEqualOrSmallerEqual(Functor arithmeticGreaterEqualOrSmallerEqual, int maxQueryCallStackCountAllowed, bool isGreaterEqual)
        {
            var stateBeforeRegistration = RegisterQueryExecution(arithmeticGreaterEqualOrSmallerEqual);

            var newQuery = _instantiatedFunctors.Push(new Functor(Functor.DisjunctionFunctorName, new ATerm[]
            {
                _instantiatedFunctors.Push(new Functor(Functor.EqualFunctorName, arithmeticGreaterEqualOrSmallerEqual.Parameters)),
                _instantiatedFunctors.Push(new Functor(isGreaterEqual ? Functor.GreaterFunctorName : Functor.SmallerFunctorName, arithmeticGreaterEqualOrSmallerEqual.Parameters)),
            }));

            foreach (var _ in ExecuteQuery(newQuery, maxQueryCallStackCountAllowed))
            {
                yield return default;
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        private IEnumerable ExecuteUnificationOrArithmeticEquality(Functor unificationOrArithmeticEquality, int maxQueryCallStackCountAllowed)

            => IsArithmeticExpression(unificationOrArithmeticEquality.Parameters[0].Dereferenced, out var leftSideValue, out int leftSideOperationsCount) &&
               IsArithmeticExpression(unificationOrArithmeticEquality.Parameters[1].Dereferenced, out var rightSideValue, out int rightSideOperationsCount) &&
               (leftSideOperationsCount > 0 || rightSideOperationsCount > 0) ?

                    ExecuteArithmeticEquality(unificationOrArithmeticEquality, leftSideValue, rightSideValue, maxQueryCallStackCountAllowed) :
                    ExecuteUnification(unificationOrArithmeticEquality, maxQueryCallStackCountAllowed);

        private IEnumerable ExecuteDisunificationOrArithmeticNonEquality(Functor disunificationOrArithmeticNonEquality)

            => IsArithmeticExpression(disunificationOrArithmeticNonEquality.Parameters[0].Dereferenced, out var leftSideValue, out int leftSideOperationsCount) &&
               IsArithmeticExpression(disunificationOrArithmeticNonEquality.Parameters[1].Dereferenced, out var rightSideValue, out int rightSideOperationsCount) &&
               (disunificationOrArithmeticNonEquality.Name != Functor.UnequalFunctorName || leftSideOperationsCount + rightSideOperationsCount > 0) ?

                    ExecuteArithmeticNonEquality(disunificationOrArithmeticNonEquality, leftSideValue, rightSideValue, disunificationOrArithmeticNonEquality.Name switch
                    {
                        Functor.UnequalFunctorName => Constraint.Type.ArithmeticInequality,
                        Functor.GreaterFunctorName => Constraint.Type.ArithmeticGreater,
                        Functor.SmallerFunctorName => Constraint.Type.ArithmeticSmaller,
                        _ => throw new NotImplementedException(), 
                    }) :
                    ExecuteDisunification(disunificationOrArithmeticNonEquality);

        private IEnumerable ExecuteUnification(Functor unification, int maxQueryCallStackCountAllowed)
        {
            var stateBeforeRegistration = RegisterQueryExecution(unification);

            if (Unify(unification.Parameters[0].Dereferenced, unification.Parameters[1].Dereferenced))
            {
                var executionStateAfterUnification = CurrentExecutionState;

                List<Functor> newQueries = new();
                HashSet<Constraint> constraints = new(); // gather all the constraints that needs to be evaluated.

                for (int i = stateBeforeRegistration.VariablesAssignedCount; i < executionStateAfterUnification.VariablesAssignedCount; i++)
                {
                    var variable = _variablesAssigned[i];
                    variable.GetConstraints(constraints);

                    if (variable.IsDelayedQuery && variable.Next is Functor query &&
                        !query.IsInCallStack && !newQueries.Contains(query))
                    {
                        newQueries.Add(query);
                    }
                }

                bool allConstraintsValid = true;
                HashSet<Constraint> discardedConstraints = new();

                foreach (var constraint in constraints)
                {
                    if (!allConstraintsValid) break;

                    var constraintLeftSideDereferenced = constraint.LeftTerm.Dereferenced;
                    var constraintRightSideDereferenced = constraint.RightTerm.Dereferenced;

                    // check if the constraint is still valid depending on the type of the constraint.
                    switch (constraint.ConstraintType)
                    {
                        case Constraint.Type.Disunification:
                            {
                                if (Unify(constraintLeftSideDereferenced, constraintRightSideDereferenced))
                                {
                                    if (_variablesAssigned.Count == executionStateAfterUnification.VariablesAssignedCount) // the two parts of the disunification are identical...
                                    {
                                        allConstraintsValid = false; // no changes to restore from since no variable assignment.
                                    }
                                    else
                                    {
                                        RestoreExecutionState(executionStateAfterUnification);
                                    }
                                }
                                else
                                {
                                    discardedConstraints.Add(constraint);
                                }
                                break;
                            }

                        case Constraint.Type.ArithmeticEquality:
                            {
                                if (IsArithmeticExpression(constraintLeftSideDereferenced, out Rational? leftSideValue, out var _) &&
                                    IsArithmeticExpression(constraintRightSideDereferenced, out Rational? rightSideValue, out var _))
                                {
                                    if ((leftSideValue.HasValue && leftSideValue.Value == Rational.InvalidRational) ||
                                        (rightSideValue.HasValue && rightSideValue.Value == Rational.InvalidRational))
                                    {
                                        allConstraintsValid = false;
                                    }
                                    else if (leftSideValue.HasValue && rightSideValue.HasValue) // all variables are instantiated.
                                    {
                                        if (leftSideValue.Value == rightSideValue.Value)
                                        {
                                            discardedConstraints.Add(constraint);
                                        }
                                        else
                                        {
                                            allConstraintsValid = false;
                                        }
                                    }
                                    else if (leftSideValue.HasValue || rightSideValue.HasValue)
                                    {
                                        // if only one variable is left to be assigned, we can derived its value.
                                        Dictionary<Variable, int> variableOccurences = new();
                                        var variableSide = rightSideValue.HasValue ? constraintLeftSideDereferenced : constraintRightSideDereferenced;
                                        variableSide.GetUnassignedVariables(variableOccurences);

                                        if (variableOccurences.Count == 1)
                                        {
                                            var variableKeyValue = variableOccurences.First();
                                            if (variableKeyValue.Value == 1)
                                            {
                                                var variableValue = ExtractVariableValue(variableKeyValue.Key, variableSide, (rightSideValue ?? leftSideValue)!.Value);
                                                
                                                if (variableValue == Rational.InvalidRational)
                                                {
                                                    allConstraintsValid = false;
                                                }
                                                else
                                                {
                                                    newQueries.Add(_instantiatedFunctors.Push(new Functor(Functor.EqualFunctorName, new ATerm[]
                                                    {
                                                        variableKeyValue.Key,
                                                        new Number(variableValue),
                                                    })));

                                                    discardedConstraints.Add(constraint);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    allConstraintsValid = false;
                                }
                                break;
                            }

                        case Constraint.Type.ArithmeticInequality:
                        case Constraint.Type.ArithmeticGreater:
                        case Constraint.Type.ArithmeticSmaller:
                            {
                                if (IsArithmeticExpression(constraintLeftSideDereferenced, out Rational? leftSideValue, out var _) &&
                                    IsArithmeticExpression(constraintRightSideDereferenced, out Rational? rightSideValue, out var _))
                                {
                                    if ((leftSideValue.HasValue && leftSideValue.Value == Rational.InvalidRational) ||
                                        (rightSideValue.HasValue && rightSideValue.Value == Rational.InvalidRational))
                                    {
                                        allConstraintsValid = false;
                                    }
                                    else if (leftSideValue.HasValue && rightSideValue.HasValue) // all variables are instantiated.
                                    {
                                        if ((constraint.ConstraintType == Constraint.Type.ArithmeticInequality && leftSideValue.Value == rightSideValue.Value) ||
                                            (constraint.ConstraintType == Constraint.Type.ArithmeticGreater && leftSideValue.Value <= rightSideValue.Value) ||
                                            (constraint.ConstraintType == Constraint.Type.ArithmeticSmaller && leftSideValue.Value >= rightSideValue.Value))
                                        {
                                            allConstraintsValid = false;
                                        }
                                        else
                                        {
                                            discardedConstraints.Add(constraint);
                                        }
                                    }
                                }
                                else
                                {
                                    allConstraintsValid = false;
                                }
                                break;
                            }
                    }
                }

                if (allConstraintsValid)
                {
                    foreach (var constraint in discardedConstraints)
                    {
                        constraint.Discarded = true;
                        _constraintsDiscarded.Push(constraint);
                    }

                    if (newQueries.Count == 0)
                    {
                        yield return default;
                    }
                    else
                    {
                        foreach (var _ in ExecuteQuery(BuildConjunctionOfQueries(newQueries, 0), maxQueryCallStackCountAllowed))
                        {
                            yield return default;
                        }
                    }
                }
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        private IEnumerable ExecuteDisunification(Functor disunification)
        {
            var stateBeforeRegistration = RegisterQueryExecution(disunification);
            var stateAfterRegistration = CurrentExecutionState;

            var disunificationLeftTermDereferenced = disunification.Parameters[0].Dereferenced;
            var disunificationRightTermDereferenced = disunification.Parameters[1].Dereferenced;
            bool doesUnify = Unify(disunificationLeftTermDereferenced, disunificationRightTermDereferenced);
            var executionStateAfterUnification = CurrentExecutionState;

            RestoreExecutionState(stateAfterRegistration);

            switch (doesUnify)
            {
                // identical left and right side. Disunification will ALWAYS fail.
                case true when executionStateAfterUnification.VariablesAssignedCount == stateAfterRegistration.VariablesAssignedCount:
                    break;

                // unification passes for now. Unification may fail (= disunification succeeding) in the future.
                case true:
                    BuildAndRegisterDisunificationConstraint(disunificationLeftTermDereferenced, disunificationRightTermDereferenced);
                    yield return default;
                    break;

                // unification fails. Disunification will always succeed.
                case false:
                    yield return default;
                    break;
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        private IEnumerable ExecuteArithmeticEquality(Functor arithmeticEquality, Rational? leftSideValue, Rational? rightSideValue, int maxQueryCallStackCountAllowed)
        {
            var stateBeforeRegistration = RegisterQueryExecution(arithmeticEquality);

            if (!((leftSideValue.HasValue && leftSideValue.Value == Rational.InvalidRational) ||
                  (rightSideValue.HasValue && rightSideValue.Value == Rational.InvalidRational)))
            {
                var leftSideDereferenced = SimplifyArithmeticExpression(arithmeticEquality.Parameters[0].Dereferenced);
                var rightSideDereferenced = SimplifyArithmeticExpression(arithmeticEquality.Parameters[1].Dereferenced);
                
                Dictionary<Variable, int> unassignedVariables = new();
                leftSideDereferenced.GetUnassignedVariables(unassignedVariables);
                rightSideDereferenced.GetUnassignedVariables(unassignedVariables);

                bool registerConstraint = false;

                if (leftSideValue.HasValue || rightSideValue.HasValue)
                {
                    switch (unassignedVariables.Count)
                    {
                        case 1:
                            var variableKeyValue = unassignedVariables.First();
                            if (variableKeyValue.Value == 1)
                            {
                                var variableSide = rightSideValue.HasValue ? leftSideDereferenced : rightSideDereferenced;
                                var variableValue = ExtractVariableValue(variableKeyValue.Key, variableSide, (rightSideValue ?? leftSideValue)!.Value);

                                if (variableValue != Rational.InvalidRational)
                                {
                                    var assignVariableQuery = _instantiatedFunctors.Push(new Functor(Functor.EqualFunctorName, new ATerm[]
                                    {
                                        variableKeyValue.Key,
                                        new Number(variableValue),
                                    }));

                                    foreach (var _ in ExecuteQuery(assignVariableQuery, maxQueryCallStackCountAllowed))
                                    {
                                        yield return default;
                                    }
                                }
                            }
                            else
                            {
                                registerConstraint = true;
                            }
                            break;

                        case 0:
                            if (leftSideValue!.Value == rightSideValue!.Value)
                            {
                                yield return default;
                            }
                            break;

                        default:
                            registerConstraint = true;
                            break;
                    }
                }
                else
                {
                    registerConstraint = true;
                }

                if (registerConstraint)
                {
                    _constraints.Push(new Constraint(leftSideDereferenced, rightSideDereferenced, unassignedVariables, Constraint.Type.ArithmeticEquality));
                    yield return default;
                }
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        // arithmetic constraints: greater, smaller and unequal.
        private IEnumerable ExecuteArithmeticNonEquality(Functor arithmeticInequality, Rational? leftSideValue, Rational? rightSideValue, Constraint.Type arithmeticNonEqualityType)
        {
            var stateBeforeRegistration = RegisterQueryExecution(arithmeticInequality);

            if (!((leftSideValue.HasValue && leftSideValue.Value == Rational.InvalidRational) ||
                  (rightSideValue.HasValue && rightSideValue.Value == Rational.InvalidRational)))
            {
                if (leftSideValue.HasValue && rightSideValue.HasValue)
                {
                    if ((arithmeticNonEqualityType == Constraint.Type.ArithmeticInequality && leftSideValue.Value != rightSideValue.Value) ||
                        (arithmeticNonEqualityType == Constraint.Type.ArithmeticGreater && leftSideValue.Value > rightSideValue.Value) ||
                        (arithmeticNonEqualityType == Constraint.Type.ArithmeticSmaller && leftSideValue.Value < rightSideValue.Value))
                    {
                        yield return default;
                    }
                }
                else
                {
                    var leftSideDereferenced = SimplifyArithmeticExpression(arithmeticInequality.Parameters[0].Dereferenced);
                    var rightSideDereferenced = SimplifyArithmeticExpression(arithmeticInequality.Parameters[1].Dereferenced);

                    Dictionary<Variable, int> unassignedVariables = new();
                    leftSideDereferenced.GetUnassignedVariables(unassignedVariables);
                    rightSideDereferenced.GetUnassignedVariables(unassignedVariables);

                    _constraints.Push(new Constraint(leftSideDereferenced, rightSideDereferenced, unassignedVariables, arithmeticNonEqualityType));
                    yield return default;
                }
            }

            RestoreExecutionState(stateBeforeRegistration);
        }

        private IEnumerable ExecuteInverseDeduction(Functor functorToBeDeduced, int maxQueryCallStackCountAllowed)
        {
            for (int i = 0; i < CurrentProgram.HornClausesAndFacts.Count; i++)
            {
                var hornClauseOrFact = CurrentProgram.HornClausesAndFacts[i];

                bool isHornClause = hornClauseOrFact.Name == Functor.HornClauseFunctorName;
                var hornClauseHeadOrFact = isHornClause ? (hornClauseOrFact.Parameters[0] as Functor)! : hornClauseOrFact;

                if (hornClauseHeadOrFact.Name == functorToBeDeduced.Name &&
                    hornClauseHeadOrFact.Arity == functorToBeDeduced.Arity)
                {
                    var stateBeforeRegistration = RegisterQueryExecution(functorToBeDeduced, false, i);

                    var compiledToRuntimeVariable = new Dictionary<Variable, Variable>();
                    var hornClauseHeadOrFactInstantiated = InstantiateFunctor(hornClauseHeadOrFact, compiledToRuntimeVariable);

                    var unification = _instantiatedFunctors.Push(new Functor(Functor.EqualFunctorName, new ATerm[]
                    {
                        functorToBeDeduced,
                        hornClauseHeadOrFactInstantiated,
                    }));

                    ATerm? hornClauseBodyInstantiated = null;

                    foreach (var _ in ExecuteQuery(unification, maxQueryCallStackCountAllowed))
                    {
                        if (isHornClause)
                        {
                            hornClauseBodyInstantiated ??= hornClauseOrFact.Parameters[1] switch
                            {
                                Variable variable => InstantiateVariable(variable, compiledToRuntimeVariable),
                                Functor functor => InstantiateFunctor(functor, compiledToRuntimeVariable),
                                Number number => new Number(number.Value),
                                _ => throw new NotImplementedException(),
                            };

                            foreach (var __ in ExecuteQuery(hornClauseBodyInstantiated.Dereferenced, maxQueryCallStackCountAllowed))
                            {
                                yield return default;
                            }
                        }
                        else // fact.
                        {
                            yield return default;
                        }
                    }

                    RestoreExecutionState(stateBeforeRegistration);
                }
            }
        }

        private Functor BuildConjunctionOfQueries(IReadOnlyList<Functor> queries, int startIndex)
            => startIndex == queries.Count - 1 ?
                queries[startIndex] :
                _instantiatedFunctors.Push(new Functor(Functor.ConjunctionFunctorName, new ATerm[]
                {
                    queries[startIndex],
                    BuildConjunctionOfQueries(queries, startIndex + 1),
                }));

        // remove subtrees that are identical between left and right terms.
        // break reference chains by dereferencing all the parameters of each functor and sub functors. 
        private void SimplifyDisunificationConstraint(ref ATerm leftTermDereferenced, ref ATerm rightTermDereferenced, out bool identicalTerms)
        {
            if (leftTermDereferenced == rightTermDereferenced ||
                (leftTermDereferenced is Number leftNumber && rightTermDereferenced is Number rightNumber && leftNumber.Value == rightNumber.Value))
            {
                identicalTerms = true;
            }
            else if (leftTermDereferenced is Functor leftFunctor && rightTermDereferenced is Functor rightFunctor) // we are sure that left and right functors unify.
            {
                List<ATerm> newLeftFunctorParameters = new();
                List<ATerm> newRightFunctorParameters = new();

                for (int i = 0; i < leftFunctor.Arity; i++)
                {
                    var leftFunctorParameter = leftFunctor.Parameters[i].Dereferenced;
                    var rightFunctorParameter = rightFunctor.Parameters[i].Dereferenced;

                    SimplifyDisunificationConstraint(ref leftFunctorParameter, ref rightFunctorParameter, out bool parametersIdentical);

                    if (!parametersIdentical)
                    {
                        newLeftFunctorParameters.Add(leftFunctorParameter);
                        newRightFunctorParameters.Add(rightFunctorParameter);
                    }
                }

                if (newLeftFunctorParameters.Count == 0)
                {
                    identicalTerms = true;
                }
                else
                {
                    leftTermDereferenced = _instantiatedFunctors.Push(new Functor(leftFunctor.Name, newLeftFunctorParameters));
                    rightTermDereferenced = _instantiatedFunctors.Push(new Functor(rightFunctor.Name, newRightFunctorParameters));
                    identicalTerms = false;
                }
            }
            else
            {
                // left and right terms cannot be simplified.
                identicalTerms = false;
            }
        }

        private void BuildAndRegisterDisunificationConstraint(ATerm leftTermDereferenced, ATerm rightTermDereferenced)
        {
            SimplifyDisunificationConstraint(ref leftTermDereferenced, ref rightTermDereferenced, out var _);

            Dictionary<Variable, int> unassignedVariables = new();
            leftTermDereferenced.GetUnassignedVariables(unassignedVariables);
            rightTermDereferenced.GetUnassignedVariables(unassignedVariables);

            _constraints.Push(new Constraint(leftTermDereferenced, rightTermDereferenced, unassignedVariables, Constraint.Type.Disunification));
        }

        // if the term is an arithmetic expression but arithmeticValue == null means that the expression constains at least one variable.
        // arithmeticValue can be NaN or Infinity.
        private static bool IsArithmeticExpression(ATerm dereferencedTerm, out Rational? arithmeticValue, out int operationsCount)
        {
            arithmeticValue = null;
            operationsCount = 0;

            switch (dereferencedTerm)
            {
                case Variable:
                    return true;

                case Number number:
                    arithmeticValue = number.Value;
                    return true;

                case Functor functor when functor.IsArithmeticExpression:
                    {
                        if (IsArithmeticExpression(functor.Parameters[0].Dereferenced, out Rational? leftParameterValue, out int leftOperationsCount) &&
                            IsArithmeticExpression(functor.Parameters[1].Dereferenced, out Rational? rightParameterValue, out int rightOperationsCount))
                        {
                            operationsCount = leftOperationsCount + rightOperationsCount + 1;

                            if (leftParameterValue.HasValue && rightParameterValue.HasValue)
                            {
                                switch (functor.Name)
                                {
                                    case Functor.AddFunctorName:
                                        arithmeticValue = leftParameterValue.Value + rightParameterValue.Value;
                                        break;
                                    case Functor.SubstractFunctorName:
                                        arithmeticValue = leftParameterValue.Value - rightParameterValue.Value;
                                        break;
                                    case Functor.MultiplyFunctorName:
                                        arithmeticValue = leftParameterValue.Value * rightParameterValue.Value;
                                        break;
                                    case Functor.DivideFunctorName:
                                        arithmeticValue = leftParameterValue.Value / rightParameterValue.Value;
                                        break;
                                }
                            }
                            
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }

                default:
                    return false;
            };
        }

        // a single instance of the variable is expected.
        private static Rational ExtractVariableValue(Variable variable, ATerm expressionContainingVariable, Rational otherSideValue)
        {
            while (expressionContainingVariable is Functor arithmeticExpressionContainingVariable)
            {
                var expressionLeftSide = arithmeticExpressionContainingVariable.Parameters[0].Dereferenced;
                var expressionRightSide = arithmeticExpressionContainingVariable.Parameters[1].Dereferenced;

                if ((expressionLeftSide is Functor leftSideAsFunctor && variable.DoesOccurInFunctor(leftSideAsFunctor)) ||
                    expressionLeftSide == variable)
                {
                    IsArithmeticExpression(expressionRightSide, out Rational? expressionRightSideValue, out var _);

                    otherSideValue = arithmeticExpressionContainingVariable.Name switch
                    {
                        Functor.AddFunctorName => otherSideValue - expressionRightSideValue!.Value,
                        Functor.SubstractFunctorName => otherSideValue + expressionRightSideValue!.Value,
                        Functor.MultiplyFunctorName => otherSideValue / expressionRightSideValue!.Value,
                        Functor.DivideFunctorName => otherSideValue * expressionRightSideValue!.Value,
                        _ => throw new NotImplementedException(),
                    };

                    expressionContainingVariable = expressionLeftSide;
                }
                else
                {
                    IsArithmeticExpression(expressionLeftSide, out Rational? expressionLeftSideValue, out var _);

                    otherSideValue = arithmeticExpressionContainingVariable.Name switch
                    {
                        Functor.AddFunctorName => otherSideValue - expressionLeftSideValue!.Value,
                        Functor.SubstractFunctorName => -otherSideValue + expressionLeftSideValue!.Value,
                        Functor.MultiplyFunctorName => otherSideValue / expressionLeftSideValue!.Value,
                        Functor.DivideFunctorName => expressionLeftSideValue!.Value / otherSideValue,
                        _ => throw new NotImplementedException(),
                    };

                    expressionContainingVariable = expressionRightSide;
                }
            }

            return otherSideValue;
        }

        // remove subtrees whose arithmetic value can be calculated right away.
        // break reference chains by dereferencing all the parameters of each functor and sub functors. 
        private ATerm SimplifyArithmeticExpression(ATerm arithmeticExpressionDereferenced)
        {
            switch (arithmeticExpressionDereferenced)
            {
                case Functor functor:
                    {
                        var parameter0 = functor.Parameters[0];
                        var parameter1 = functor.Parameters[1];

                        var parameter0Simplified = SimplifyArithmeticExpression(parameter0.Dereferenced);
                        var parameter1Simplified = SimplifyArithmeticExpression(parameter1.Dereferenced);

                        return parameter0Simplified is Number number0 && parameter1Simplified is Number number1 ?

                            new Number(functor.Name switch
                            {
                                Functor.AddFunctorName => number0.Value + number1.Value,
                                Functor.SubstractFunctorName => number0.Value - number1.Value,
                                Functor.MultiplyFunctorName => number0.Value * number1.Value,
                                Functor.DivideFunctorName => number0.Value / number1.Value,
                                _ => throw new NotImplementedException(),
                            }) :

                            (parameter0 != parameter0Simplified || parameter1 != parameter1Simplified ?

                                _instantiatedFunctors.Push(new Functor(functor.Name, new ATerm[]
                                {
                                    parameter0Simplified,
                                    parameter1Simplified,
                                })) :

                                functor);
                    }

                default:
                    return arithmeticExpressionDereferenced;
            };
        }

        #endregion
    }
}