﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Qwack.Math.Interpolation;
using Xunit;

namespace Qwack.Math.Tests.Interpolation
{
    public class LinearInterpolatorFacts
    {
        [Fact]
        public void CanInterpolateFact()
        {
            var interp = new LinearInterpolator(new double[] { 0, 10 }, new double[] { 10, 10 });
            Assert.Equal(10.0,  interp.Interpolate(100.0));
        }

        [Fact]
        public void LinearInterpolatorTests_CanExtrapolate()
        {
            IInterpolator1D interp = new LinearInterpolator(new double[] { 5, 10 }, new double[] { 5, 10 });
            Assert.Equal(0, interp.Interpolate(0.0));
            Assert.Equal(7.5, interp.Interpolate(7.5));
            Assert.Equal(100, interp.Interpolate(100));
            Assert.Equal(1, interp.FirstDerivative(0));
            Assert.Equal(1, interp.FirstDerivative(5));
            Assert.Equal(1, interp.FirstDerivative(100));
            Assert.Equal(0, interp.SecondDerivative(0));
            Assert.Equal(1, interp.SecondDerivative(5));
            Assert.Equal(0, interp.SecondDerivative(100));

            interp= new LinearInterpolatorFlatExtrap(new double[] { 5, 10 }, new double[] { 5, 10 });
            Assert.Equal(5, interp.Interpolate(0.0));
            Assert.Equal(7.5, interp.Interpolate(7.5));
            Assert.Equal(10, interp.Interpolate(100));
            Assert.Equal(0, interp.FirstDerivative(0));
            Assert.Equal(1, interp.FirstDerivative(5));
            Assert.Equal(0, interp.FirstDerivative(100));
            Assert.Equal(0, interp.SecondDerivative(0));
            Assert.Equal(0.5, interp.SecondDerivative(5));
            Assert.Equal(0, interp.SecondDerivative(100));
        }
    }
}
