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
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public readonly struct Rational
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
#pragma warning restore CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
    {
        public readonly long Numerator; // rational sign specified in the numerator.
        public readonly long Denominator; // always positive if valid rational. Denominator == 0 means invalid rational.

        public static readonly Rational InvalidRational = new Rational(0, 0);
        public static readonly Rational Zero = new Rational(0, 1);
        public static readonly Rational One = new Rational(1, 1);

        public Rational(long numerator, long denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            
            if (Denominator < 0)
            {
                // make sure the sign of the rational is set in the numerator.
                Numerator *= -1;
                Denominator *= -1; // > 0
            }

            if (Denominator == 0)
            {
                Numerator = 0; // this is an invalid rational.
            }
            else if (Numerator == 0)
            {
                Denominator = 1; // normalized zero.
            }
            else // Denominator, Abs(Numerator) > 0 
            {     
                long gcd = GreatestCommonDivisor(Math.Abs(Numerator), Denominator);
                Numerator /= gcd;
                Denominator /= gcd;
            }
        }

        public Rational(long integer) : this(integer, 1) { }

        // number1, number2 > 0.
        private static long GreatestCommonDivisor(long number1, long number2)
        {
            if (number1 % number2 == 0)
            {
                return number2;
            }
            else if (number2 % number1 == 0)
            {
                return number1;
            }
            else
            {
                while (number1 != number2)
                {
                    if (number1 > number2)
                        number1 -= number2;
                    else
                        number2 -= number1;
                }

                return number1;
            }
        }

        public static Rational operator -(Rational rational)
            => new Rational(-rational.Numerator, rational.Denominator);

        public static Rational operator +(Rational left, Rational right)
            => new Rational(left.Numerator * right.Denominator + right.Numerator * left.Denominator, left.Denominator * right.Denominator);

        public static Rational operator -(Rational left, Rational right)
            => new Rational(left.Numerator * right.Denominator - right.Numerator * left.Denominator, left.Denominator * right.Denominator);

        public static Rational operator *(Rational left, Rational right)
            => new Rational(left.Numerator * right.Numerator, left.Denominator * right.Denominator);

        public static Rational operator /(Rational left, Rational right)
            => new Rational(left.Numerator * right.Denominator, left.Denominator * right.Numerator);

        public static bool operator ==(Rational left, Rational right)
            => left.Numerator == right.Numerator && left.Denominator == right.Denominator;

        public static bool operator !=(Rational left, Rational right)
            => left.Numerator != right.Numerator || left.Denominator != right.Denominator;

        public static bool operator >(Rational left, Rational right)
            => (left == InvalidRational || right == InvalidRational) ? throw new Exception() : (left - right).Numerator > 0;

        public static bool operator <(Rational left, Rational right)
            => (left == InvalidRational || right == InvalidRational) ? throw new Exception() : (left - right).Numerator < 0;

        public static bool operator >=(Rational left, Rational right)
            => (left == InvalidRational || right == InvalidRational) ? throw new Exception() : (left - right).Numerator >= 0;

        public static bool operator <=(Rational left, Rational right)
            => (left == InvalidRational || right == InvalidRational) ? throw new Exception() : (left - right).Numerator <= 0;
    }
}