# Symbolic.ai

A prolog-inspired inference engine written in modern C# and targeting .NET6.

## Key similarities with prolog:

1. A program is simply composed by facts and production rules (horn clauses).
2. Unification of terms (T1 = T2) and inverse deduction (T1 :- T2, ..., Tn) are the two core algorithms of the inference engine.
3. Built-in backtracking so all the valid solutions of a given query can be retrieved.
4. Built-in negation as failure.
5. The inference engine purpose is to find solutions to queries (goals) introduced by the user.

## Key differences with prolog:

1. Removal of logical unpure constructs such as the cut (!).
2. Instead of prolog simple depth-first search strategy, symbolic.ai uses an iterative deepening approach. Iterative deepening allows the retrieval of a valid solution in contexts where prolog would run indefinitely before finding that solution. Quite common when using left recursion.
3. Built-in disunification (diff) constraints. Query "X != Y" with unbound variables X and Y succeeds. Query "X != Y, X = 5, Y = 5" fails.
4. Delayed queries constraints. No more prolog errors such as: "ERROR: Arguments are not sufficiently instantiated" which is logically unsound. In symbolic.ai, calling an unbound variable as query succeeds optimistically and in the future when that variable unify with a functor, that functor will be called as a query (if that new query does not succeeds, backtracking will occur). Consider as an example the program containing one fact: "myProvableFact()" and two production rules "myProductionRule1() :- X, X = myProvableFact()" and "myProductionRule2() :- X, X = myUnprovableFact()". While, user query "myProductionRule1()" yields one solution, query "myProductionRule2()" yields no solution. Both queries execute without throwing any errors.
5. Arithmetic constraints over rational numbers: =, !=, >, <, <=, >, >=. Similarly with 3. and 4, these queries can succeed optimistically. When succeeding optimistically, these constraints will have impact, at a later stage during the inference process, on which goals yield or not solutions. As an example, in query "X + Y != 10, X = 4, Y = 6" while the first two inferences steps pass: step 1. the constraint "!=" passes optimistically, step 2. "X" unifies with "4", step 3. fails because if "Y" unified with "6", assumption that step 1 passes (10 != 10) would have been proven wrong. Therefore query: "X + Y != 10, X = 4, Y = 6" has no solution.
6. As the reader can understand from the 5 points above, contrary to prolog, in symbolic.ai the programmer can abstract himself about how internally the inference engine would execute its query. The programmer can therefore stop thinking procedurally and instead start thinking only from a logical point of view. The position of any goal "Gi" in a production rule "P := G1, G2, ..., Gn" no longer matters (besides from a performance point of view, obviously).

## How can I get started using symbolic.ai?

The solution contains two projects. Project "InferenceEngine" is the inference engine itself while project "InferenceEngineTests" contains some test cases that ilustrates which public API from "InferenceEngine" is expected to be called when setting up logic programs and preparing queries to be executed.

## Current limitations

1. Due to be an highly dynamic and interpreted inference engine, runtime performance is significantly lower than any WAM-based prolog implementation such as SWI-PROLOG. Performance can therefore be improved by adding object pooling, for example.
2. Lack of a standard library to operate on list, io, etc. Any contribution is welcomed!
3. Programs and queries are only expected to be built programmatically. Developing a parser would also be a great contribution.
4. The arithmetic constraints over rationals have potential to become way smarter. They are still quite simple. To understand their behaviour let's look at another query example: "X + 2 * Y = 50 - Z, Y = 30, Z = 10". In this case, only after the third step, when only one variable ("X") remains to be assigned in the constraint, symbolic.ai will extract the exact value for that variable. In many situations, values for 2 or more variables could have been extracted simulatenously; this behaviour is not yet supported by the engine. System of linear equations are a good example. Consider the following query that represents a system of linear equations with two variables: "3X + 2Y = 50, 6X - Y = -20". Values for variables "X" and "Y" could have been extracted right after the registration of the two constraints: no need to wait at a later stage in the inference proccess to extract the value of "Y" (or "X") after "X" (or "Y") has been unified with a number. Despite their current limitations, arithmetic constraints in symbolic.ai are powerful enough to handle many common situations. An already supported example is to solve factorials in both directions, i.e, find output given input or find input given output (see test case factorial in "InferenceEngineTests").
