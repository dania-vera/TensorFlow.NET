﻿//using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tensorflow.Gradients
{
    /// <summary>
    /// Gradients for operators defined in math_ops.py.
    /// </summary>
    public class math_grad : Python
    {
        public static Tensor[] _AddGrad(Operation op, Tensor[] grads)
        {
            var x = op.inputs[0];
            var y = op.inputs[1];
            var grad = grads[0];
            if (grad is Tensor && _ShapesFullySpecifiedAndEqual(x, y, grad))
                return new Tensor[] { grad, grad };

            var sx = array_ops.shape(x);
            var sy = array_ops.shape(y);
            var (rx, ry) = gen_array_ops.broadcast_gradient_args(sx, sy);

            var sum1 = math_ops.reduce_sum(grad, rx);
            var r1 = gen_array_ops.reshape(sum1, sx);
            var sum2 = math_ops.reduce_sum(grad, ry);
            var r2 = gen_array_ops.reshape(sum2, sy);

            return new Tensor[] { r1, r2 };
        }

        public static Tensor[] _IdGrad(Operation op, Tensor[] grads)
        {
            return new Tensor[] { grads[0] };
        }

        public static Tensor[] _LogGrad(Operation op, Tensor[] grads)
        {
            var grad = grads[0];
            var x = op.inputs[0];
            return with(ops.control_dependencies(new Operation[] { grad }), dp => {
                x = math_ops.conj(x);
                return new Tensor[] { grad * math_ops.reciprocal(x) };
            });
        }

        public static Tensor[] _MulGrad(Operation op, Tensor[] grads)
        {
            var x = op.inputs[0];
            var y = op.inputs[1];
            var grad = grads[0];
            if (grad is Tensor && 
                _ShapesFullySpecifiedAndEqual(x, y, grad) &&
                new TF_DataType[] { tf.int32, tf.float32 }.Contains(grad.dtype))
                return new Tensor[] { gen_math_ops.mul(grad, y), gen_math_ops.mul(grad, x) };

            var sx = array_ops.shape(x);
            var sy = array_ops.shape(y);
            var (rx, ry) = gen_array_ops.broadcast_gradient_args(sx, sy);

            x = math_ops.conj(x);
            y = math_ops.conj(y);

            var mul1 = gen_math_ops.mul(grad, y);
            var reduce_sum1 = math_ops.reduce_sum(mul1, rx);
            var reshape1 = gen_array_ops.reshape(reduce_sum1, sx);

            var mul2 = gen_math_ops.mul(x, grad);
            var reduce_sum2 = math_ops.reduce_sum(mul2, ry);
            var reshape2 = gen_array_ops.reshape(reduce_sum2, sy);

            return new Tensor[] { reshape1, reshape2 };
        }

        public static Tensor[] _MatMulGrad(Operation op, Tensor[] grads)
        {
            var grad = grads[0];
            Tensor grad_a = null, grad_b = null;

            var t_a = (bool)op.get_attr("transpose_a");
            var t_b = (bool)op.get_attr("transpose_b");
            var a = math_ops.conj(op.inputs[0]);
            var b = math_ops.conj(op.inputs[1]);
            if(!t_a && !t_b)
            {
                grad_a = gen_math_ops.mat_mul(grad, b, transpose_b: true);
                grad_b = gen_math_ops.mat_mul(a, grad, transpose_a: true);
            }
            else if (!t_a && t_b)
            {
                grad_a = gen_math_ops.mat_mul(grad, b);
                grad_b = gen_math_ops.mat_mul(grad, a, transpose_a: true);
            }
            else if (t_a && !t_b)
            {
                grad_a = gen_math_ops.mat_mul(grad, b);
                grad_b = gen_math_ops.mat_mul(grad, a, transpose_a: true);
            }
            else if (t_a && t_b)
            {
                grad_a = gen_math_ops.mat_mul(b, grad, transpose_a: true, transpose_b: true);
                grad_b = gen_math_ops.mat_mul(grad, a, transpose_a: true, transpose_b: true);
            }

            return new Tensor[] { grad_a, grad_b };
        }

        public static Tensor[] _MeanGrad(Operation op, Tensor[] grads)
        {
            var grad = grads[0];
            var sum_grad = _SumGrad(op, grads)[0];
            var input_shape = op.inputs[0]._shape_tuple();
            var output_shape = op.outputs[0]._shape_tuple();

            var input_shape_tensor = array_ops.shape(op.inputs[0]);
            var output_shape_tensor = array_ops.shape(op.outputs[0]);
            var factor = _safe_shape_div(math_ops.reduce_prod(input_shape_tensor), math_ops.reduce_prod(output_shape_tensor));

            return new Tensor[] { math_ops.truediv(sum_grad, math_ops.cast(factor, sum_grad.dtype)), null };
        }

        public static Tensor[] _NegGrad(Operation op, Tensor[] grads)
        {
            return new Tensor[] { -grads[0] };
        }

        private static Tensor _safe_shape_div(Tensor x, Tensor y)
        {
            return math_ops.floordiv(x, gen_math_ops.maximum(y, 1));
        }

        public static Tensor[] _SubGrad(Operation op, Tensor[] grads)
        {
            var grad = grads[0];
            var x = op.inputs[0];
            var y = op.inputs[1];
            if (grad is Tensor && _ShapesFullySpecifiedAndEqual(x, y, grad))
                return new Tensor[] { grad, -grad };

            var sx = array_ops.shape(x);
            var sy = array_ops.shape(y);
            var (rx, ry) = gen_array_ops.broadcast_gradient_args(sx, sy);

            var r1 = gen_array_ops.reshape(math_ops.reduce_sum(grad, rx), sx);
            var r2 = gen_array_ops.reshape(-math_ops.reduce_sum(grad, ry), sy);

            return new Tensor[] { r1, r2 };
        }

        public static bool _ShapesFullySpecifiedAndEqual(Tensor x, Tensor y, Tensor grad)
        {
            var x_shape = x._shape_tuple();
            var y_shape = y._shape_tuple();
            var grad_shape = grad._shape_tuple();
            return Enumerable.SequenceEqual(x_shape, y_shape) &&
                Enumerable.SequenceEqual(y_shape, grad_shape) &&
                x.NDims != -1 &&
                !x_shape.Contains(-1);
        }

        public static Tensor[] _SumGrad(Operation op, Tensor[] grads)
        {
            var grad = grads[0];
            var input_0_shape = op.inputs[0]._shape_tuple();
            Tensor input_shape = null;

            if (input_0_shape != null)
            {
                var axes = tensor_util.constant_value(op.inputs[1]);
                if(!(axes is null))
                {
                    var rank = input_0_shape.Length;
                    if (Enumerable.SequenceEqual(Enumerable.Range(0, rank), axes.Data<int>()))
                    {
                        grad = array_ops.reshape(grad, new int[] { 1 });
                        if (!input_0_shape.Contains(-1))
                            input_shape = constant_op.constant(input_0_shape);
                        else
                            input_shape = array_ops.shape(op.inputs[0]);
                        return new Tensor[] { gen_array_ops.tile(grad, input_shape), null };
                    }
                }
            }

            input_shape = array_ops.shape(op.inputs[0]);
            ops.colocate_with(input_shape);
            var output_shape_kept_dims = math_ops.reduced_shape(input_shape, op.inputs[1]);
            var tile_scaling = _safe_shape_div(input_shape, output_shape_kept_dims);
            grad = gen_array_ops.reshape(grad, output_shape_kept_dims);

            return new Tensor[] { gen_array_ops.tile(grad, tile_scaling), null };
        }

        public static Tensor[] _RealDivGrad(Operation op, Tensor[] grads)
        {
            var grad = grads[0];
            var x = op.inputs[0];
            var y = op.inputs[1];

            var sx = array_ops.shape(x);
            var sy = array_ops.shape(y);
            var (rx, ry) = gen_array_ops.broadcast_gradient_args(sx, sy);
            x = math_ops.conj(x);
            y = math_ops.conj(y);

            var realdiv1 = gen_math_ops.real_div(-x, y);
            var realdiv2 = gen_math_ops.real_div(realdiv1, y);
            var reduce_sum1 = math_ops.reduce_sum(grad * realdiv2, ry);
            var reshape1 = gen_array_ops.reshape(reduce_sum1, sy);
            var realdiv3 = gen_math_ops.real_div(grad, y);
            var reduce_sum2 = math_ops.reduce_sum(realdiv3, rx);
            var reshape2 = gen_array_ops.reshape(reduce_sum2, sx);

            return new Tensor[] { reshape2, reshape1 };
        }

        public static Tensor[] _PowGrad(Operation op, Tensor[] grads)
        {
            var grad = grads[0];
            var x = op.inputs[0];
            var y = op.inputs[1];
            var z = op.outputs[0];

            var sx = array_ops.shape(x);
            var sy = array_ops.shape(y);
            var (rx, ry) = gen_array_ops.broadcast_gradient_args(sx, sy);
            x = math_ops.conj(x);
            y = math_ops.conj(y);
            z = math_ops.conj(z);
            var pow = gen_math_ops.pow(x, y - 1.0f);
            var mul = grad * y * pow;
            var reduce_sum = math_ops.reduce_sum(mul, rx);
            var gx = gen_array_ops.reshape(reduce_sum, sx);

            // Avoid false singularity at x = 0
            Tensor mask = null;
            if (x.dtype.is_complex())
                throw new NotImplementedException("x.dtype.is_complex()");
            else
                mask = x > 0.0f;
            var ones = array_ops.ones_like(x);
            var safe_x = array_ops.where(mask, x, ones);
            var x1 = gen_array_ops.log(safe_x);
            var y1 = array_ops.zeros_like(x);
            var log_x = array_ops.where(mask, x1, y1);
            var mul1 = grad * z * log_x;
            var reduce_sum1 = math_ops.reduce_sum(mul1, ry);
            var gy = gen_array_ops.reshape(reduce_sum1, sy);

            return new Tensor[] { gx, gy };
        }
    }
}
