﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Luminance.Common.Easings
{
    public class PiecewiseRotation
    {
        /// <summary>
        /// A piecewise rotation curve that takes up a part of the domain of a <see cref="PiecewiseCurve"/>, specifying the equivalent range and curvature in said domain.
        /// </summary>
        public record CurveSegment(Quaternion StartingRotation, Quaternion EndingRotation, float AnimationStart, float AnimationEnd, EasingCurves.Curve Curve, EasingType CurveType);

        /// <summary>
        /// The list of <see cref="CurveSegment"/> that encompass the entire 0-1 domain of this function.
        /// </summary>
        protected List<CurveSegment> segments = [];

        public PiecewiseRotation Add(EasingCurves.Curve curve, EasingType curveType, Quaternion endingRotation, float animationEnd, Quaternion? startingRotation = null)
        {
            float animationStart = segments.Any() ? segments.Last().AnimationEnd : 0f;
            startingRotation ??= segments.Any() ? segments.Last().EndingRotation : Quaternion.Identity;
            if (animationEnd <= 0f || animationEnd > 1f)
                throw new InvalidOperationException("A piecewise animation curve segment cannot have a domain outside of 0-1.");

            // Add the new segment.
            segments.Add(new(startingRotation.Value, endingRotation, animationStart, animationEnd, curve, curveType));

            // Return the piecewise curve that called this method to allow method chaining.
            return this;
        }

        public Quaternion Evaluate(float interpolant, bool takeOptimalRoute, int inversionDirection)
        {
            // Clamp the interpolant into the valid range.
            interpolant = Saturate(interpolant);

            // Calculate the local interpolant relative to the segment that the base interpolant fits into.
            CurveSegment segmentToUse = segments.Find(s => interpolant >= s.AnimationStart && interpolant <= s.AnimationEnd);
            float curveLocalInterpolant = InverseLerp(segmentToUse.AnimationStart, segmentToUse.AnimationEnd, interpolant);

            // Calculate the segment value based on the local interpolant.
            float segmentInterpolant = segmentToUse.Curve.Evaluate(segmentToUse.CurveType, 0f, 1f, curveLocalInterpolant);

            // Spherically interpolate piecemeal between the quaternions.
            // Unlike a single Quaternion.Lerp, which would typically invert negative dot products, this has the ability to take un-optimal routes to the destination angle, which is desirable for things such as big swings.
            Quaternion start = segmentToUse.StartingRotation;
            Quaternion end = segmentToUse.EndingRotation;

            start.Normalize();
            end.Normalize();
            float similarity = Quaternion.Dot(start, end);
            if (similarity.NonZeroSign() != inversionDirection && takeOptimalRoute)
            {
                similarity *= -1f;
                start *= -1f;
            }

            float angle = Acos(Clamp(similarity, -0.9999f, 0.9999f));
            float cosecantAngle = 1f / Sin(angle);
            return (start * Sin((1f - segmentInterpolant) * angle) + end * Sin(segmentInterpolant * angle)) * cosecantAngle;
        }
    }
}
