"""Check the proposed RMS error-model inequalities, not a runtime implementation."""

from fractions import Fraction
from math import isqrt
import struct


def power_of_two(exponent):
    if exponent >= 0:
        return Fraction(2**exponent)
    return Fraction(1, 2**-exponent)


def sqrt_interval(value):
    scale = 1 << 160
    root = isqrt(value.numerator * scale * scale // value.denominator)
    lower = Fraction(root, scale)
    upper = Fraction(root + 1, scale)
    assert lower * lower <= value < upper * upper
    return lower, upper


def verify():
    unit_roundoff = power_of_two(-24)
    smallest_normal = power_of_two(-126)
    underflow_allowance = power_of_two(-55)
    guard_width = power_of_two(-10)
    maximum_raw_bound = Fraction(0)
    maximum_meter_bound = Fraction(0)

    for count in range(32, 4097):
        depth = count + 16
        gamma = depth * unit_roundoff / (1 - depth * unit_roundoff)
        lower = (1 - unit_roundoff) ** 4 * sqrt_interval(1 - gamma)[0]
        upper = (1 + unit_roundoff) ** 4 * sqrt_interval(1 + gamma)[1]
        raw_bound = upper - lower + 2 * underflow_allowance
        meter_bound = (
            3 * raw_bound
            + 6 * unit_roundoff * (upper + underflow_allowance)
        )
        tiny_mean_bound = (
            8 * depth * smallest_normal
            / (count * (1 - depth * unit_roundoff))
            + smallest_normal
        )

        assert tiny_mean_bound < (underflow_allowance / 2) ** 2
        assert raw_bound < Fraction(1, 4000)
        assert meter_bound < power_of_two(-10)
        maximum_raw_bound = max(maximum_raw_bound, raw_bound)
        maximum_meter_bound = max(maximum_meter_bound, meter_bound)

        if count == 512:
            assert raw_bound < power_of_two(-14)
            for decimal_threshold in (0.03, 0.008):
                float_threshold = struct.unpack(
                    "<f", struct.pack("<f", decimal_threshold)
                )[0]
                threshold = Fraction.from_float(float_threshold)
                scalar_upper_below = (
                    upper / lower * threshold * (1 - guard_width)
                    + underflow_allowance * (1 + upper / lower)
                )
                scalar_lower_above = (
                    lower / upper * threshold * (1 + guard_width)
                    - underflow_allowance * (1 + lower / upper)
                )
                assert scalar_upper_below < threshold
                assert scalar_lower_above > threshold
                print(
                    f"Both strict guard implications pass for "
                    f"float threshold {float_threshold:.12g}."
                )

    print("Verified every allowed length from 32 through 4096 inclusive.")
    print(f"Maximum raw-RMS bound: {float(maximum_raw_bound):.12g}")
    print(f"Maximum rounded-meter bound: {float(maximum_meter_bound):.12g}")
    print(
        "Conditional on the supplied operation-depth/error model. "
        "This does not verify TensorPrimitives, JIT code, "
        "floating-point control state, or pipeline behavior."
    )


if __name__ == "__main__":
    verify()
