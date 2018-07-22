﻿using ILNumerics;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp.Dev.Statistics
{
    class MultiDimensionalScaling
    {
        /// <summary>
        /// Multidimensional scaling/PCoA: transform distances to points in a coordinate system.
        /// </summary>
        /// <param name="input">A matrix of pairwise distances. Zero indicates identical objects.</param>
        /// <returns>A matrix, the columns of which are coordinates in the nth dimension. 
        /// The rows are in the same order as the input.</returns>
        public static ILArray<double> Scale(ILArray<double> input)
        {
            int n = input.Length;

            ILArray<double> p = ILMath.eye<double>(n, n) - ILMath.repmat(1.0 / n, n, n);

            ILArray<double> a = -.5 * ILMath.multiplyElem(input, input);
            ILArray<double> b = ILMath.multiply(p, a, p);

            ILArray<complex> V = ILMath.empty<complex>();
            ILArray<complex> E = ILMath.eig((b + b.T) / 2, V);

            ILArray<int> i = ILMath.empty<int>();
            ILArray<double> e = ILMath.sort(ILMath.diag(ILMath.real(E)), i);

            e = ILMath.flipud(e);
            i = ILMath.toint32(ILMath.flipud(ILMath.todouble(i)));

            ILArray<int> keep = ILMath.empty<int>();
            for (int j = 0; j < e.Length; j++)
            {
                if (e[j] > 0.000000001)
                {
                    keep.SetValue(j, keep.Length);
                }
            }

            ILArray<double> Y;
            if (ILMath.isempty(keep))
            {
                Y = ILMath.zeros(n, 1);
            }
            else
            {
                Y = ILMath.zeros<double>(V.S[0], keep.Length);
                for (int j = 0; j < keep.Length; j++)
                {
                    Y[ILMath.full, j] = ILMath.todouble(-V[ILMath.full, i[keep[j]]]);
                }
                Y = ILMath.multiply(Y, ILMath.diag(ILMath.sqrt(e[keep])));
            }

            ILArray<int> maxind = ILMath.empty<int>();
            ILMath.max(ILMath.abs(Y), maxind, 0);
            int d = Y.S[1];
            ILArray<int> indices = maxind + ILMath.toint32(ILMath.array<int>(SteppedRange(0, n, (d - 1) * n)));
            ILArray<double> colsign = ILMath.sign(Y[indices]);
            for (int j = 0; j < Y.S[1]; j++)
            {
                Y[ILMath.full, j] = Y[ILMath.full, j] * colsign[j];
            }

            return Y;
        }

        private static int[] SteppedRange(int start, int step, int end)
        {
            var values = new List<int>();
            for (int i = start; i <= end; i = i + step)
            {
                values.Add(i);
            }
            return values.ToArray();
        }
    }
}
