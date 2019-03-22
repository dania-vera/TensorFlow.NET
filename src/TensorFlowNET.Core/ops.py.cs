﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Tensorflow;
using node_def_pb2 = Tensorflow;
using Google.Protobuf;
using System.Linq;
using NumSharp.Core;
using System.ComponentModel;
using Tensorflow.Gradients;

namespace Tensorflow
{
    public partial class ops : Python
    {
        public static void add_to_collection<T>(string name, T value)
        {
            var graph = tf.get_default_graph();
            graph.add_to_collection(name, value);
        }

        public static void add_to_collections<T>(List<string> names, T value)
        {
            var graph = tf.get_default_graph();
            graph.add_to_collections(names, value);
        }

        /// <summary>
        /// Wrapper for `Graph.get_collection()` using the default graph.
        /// contains many standard names for collections.
        /// </summary>
        /// <param name="key">
        /// The key for the collection. For example, the `GraphKeys` class
        /// </param>
        /// <param name="scope"></param>
        /// <returns>
        /// The list of values in the collection with the given `name`, or
        /// an empty list if no value has been added to that collection. The
        /// list contains the values in the order under which they were
        /// collected.
        /// </returns>
        public static object get_collection(string key, string scope = "")
        {
            return get_default_graph().get_collection(key, scope);
        }

        public static object get_collection_ref(string key)
        {
            return get_default_graph().get_collection_ref(key);
        }

        private static Graph default_graph;
        public static Graph get_default_graph()
        {
            if (default_graph == null)
                default_graph = tf.Graph();
            return default_graph;
        }
        public static Graph set_default_graph(Graph graph)
        {
            default_graph = graph;
            return default_graph;
        }

        public static Graph _get_graph_from_inputs(List<Tensor> op_input_list, Graph graph = null)
        {
            foreach(var op_input in op_input_list)
            {
                // Determine if this is a valid graph_element.
                var graph_element = op_input;
            }

            return get_default_graph();
        }

        /// <summary>
        /// Converts the given `value` to a `Tensor`.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="dtype"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Tensor convert_to_tensor(object value, TF_DataType dtype = TF_DataType.DtInvalid, string name = null, TF_DataType preferred_dtype = TF_DataType.DtInvalid)
        {
            return convert_to_tensor_v2(value, dtype, preferred_dtype, name);
        }

        public static Tensor convert_to_tensor_v2(object value, TF_DataType dtype = TF_DataType.DtInvalid, TF_DataType dtype_hint = TF_DataType.DtInvalid, string name = null)
        {
            return internal_convert_to_tensor(value, dtype: dtype, name: name, preferred_dtype: dtype_hint, as_ref: false);
        }

        public static Tensor convert_to_tensor_or_composite(Tensor value, TF_DataType dtype = TF_DataType.DtInvalid, string name = null)
        {
            return internal_convert_to_tensor_or_composite(value: value, dtype: dtype, name: name, as_ref: false);
        }

        public static Tensor internal_convert_to_tensor_or_composite(Tensor value, TF_DataType dtype = TF_DataType.DtInvalid, string name = null, bool as_ref = false)
        {
            return internal_convert_to_tensor(value, dtype: dtype, name: name, as_ref: as_ref);
        }

        /// <summary>
        /// Wrapper for `Graph.control_dependencies()` using the default graph.
        /// </summary>
        /// <param name="control_inputs"></param>
        public static _ControlDependenciesController control_dependencies(Operation[] control_inputs)
        {
            return get_default_graph().control_dependencies(control_inputs);
        }

        /// <summary>
        /// Creates a TF_Operation.
        /// </summary>
        /// <param name="graph">a `Graph`.</param>
        /// <param name="node_def">`node_def_pb2.NodeDef` for the operation to create.</param>
        /// <param name="inputs">
        /// A list of `Tensor`s (corresponding to scalar inputs) and lists of
        /// `Tensor`s (corresponding to sequence inputs, e.g. "int64 * N",
        /// "list(int64)"). The length of the list should be equal to the number of
        /// inputs specified by this operation's op def.
        /// </param>
        /// <param name="control_inputs">A list of `Operation`s to set as control dependencies.</param>
        /// <returns>A wrapped TF_Operation*.</returns>
        public static IntPtr _create_c_op<T>(Graph graph, NodeDef node_def, T[] inputs, Operation[] control_inputs)
        {
            var op_desc = graph.NewOperation(node_def.Op, node_def.Name);

            // Add inputs
            foreach (var op_input in inputs)
            {
                if (op_input is Tensor[] op_inputs)
                    c_api.TF_AddInputList(op_desc, op_inputs.Select(x => x._as_tf_output()).ToArray(), op_inputs.Length);
                else if (op_input is Tensor op_input1)
                {
                    if (op_input1.op == null)
                        c_api.TF_AddInput(op_desc, new TF_Output(op_desc, 0));
                    else
                        c_api.TF_AddInput(op_desc, op_input1._as_tf_output());
                }
                else
                    throw new NotImplementedException("_create_c_op");
            }

            var status = new Status();

            // Add control inputs
            foreach (var control_input in control_inputs)
                c_api.TF_AddControlInput(op_desc, control_input);

            // Add attrs
            foreach (var attr in node_def.Attr)
            {
                var bytes = attr.Value.ToByteArray();
                var proto = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, proto, bytes.Length);
                uint len = (uint)bytes.Length;
                c_api.TF_SetAttrValueProto(op_desc, attr.Key, proto, proto_len: len, status: status);

                status.Check(true);
            }

            var c_op = c_api.TF_FinishOperation(op_desc, status);

            status.Check(true);

            return c_op;
        }

        public static OpDef _get_op_def(Graph graph, string type)
        {
            return graph.GetOpDef(type);
        }

        public static NodeDef _NodeDef(string op_type, string name, string device = "", Dictionary<string, AttrValue> attrs = null)
        {
            var node_def = new node_def_pb2.NodeDef();
            node_def.Op = op_type;
            node_def.Name = name;

            foreach (var attr in attrs)
            {
                node_def.Attr.Add(attr.Key, attr.Value);
            }
            
            return node_def;
        }

        public static string _name_from_scope_name(string name)
        {
            if (name.EndsWith("/"))
            {
                return name.Substring(0, name.Length - 1);
            }
            else
            {
                return name;
            }
        }

        /// <summary>
        /// A context manager that lifts ops out of control-flow scopes and function-building graphs.
        /// </summary>
        /// <returns></returns>
        public static void init_scope()
        {
            // Retrieve the active name scope: entering an `init_scope` preserves
            // the name scope of the current context.
            var default_graph = get_default_graph();
            var scope = default_graph.get_name_scope();
            if (!String.IsNullOrEmpty(scope) && !scope.EndsWith("/"))
                // Names that end with trailing slashes are treated by `name_scope` as
                // absolute.
                scope += "/";
            // inner_device_stack = default_graph._device_function_stack
            // var outer_context = default_graph.as_default;

            with(ops.control_dependencies(null), delegate
            {
                var outer_graph = get_default_graph();
                // outer_device_stack = None
            });
        }

        private static int uid_number = 0;

        /// <summary>
        /// A unique (within this program execution) integer.
        /// Not thread safe
        /// </summary>
        /// <returns></returns>
        public static int uid()
        {
            return uid_number++;
        }

        public static void colocate_with(Operation op, bool ignore_existing = false)
        {
            _colocate_with_for_gradient(op, null, ignore_existing);
        }

        public static void colocate_with(Tensor tensor, bool ignore_existing = false)
        {
            _colocate_with_for_gradient(tensor.op, null, ignore_existing);
        }

        public static void _colocate_with_for_gradient(Operation op, string gradient_uid, bool ignore_existing = false)
        {
            var default_graph = get_default_graph();
            default_graph._colocate_with_for_gradient(op, gradient_uid, ignore_existing);
        }

        /// <summary>
        /// Uses the default session to evaluate one or more tensors.
        /// </summary>
        /// <param name="tensors">A single Tensor, or a list of Tensor objects.</param>
        /// <param name="feed_dict">
        /// A dictionary that maps Tensor objects (or tensor names) to lists,
        /// numpy ndarrays, TensorProtos, or strings.
        /// </param>
        /// <param name="graph">The graph in which the tensors are defined.</param>
        /// <param name="session">A different session to use to evaluate "tensors".</param>
        /// <returns>
        /// Either a single numpy ndarray if "tensors" is a single tensor; or a list
        /// of numpy ndarrays that each correspond to the respective element in
        /// "tensors".
        /// </returns>
        public static NDArray _eval_using_default_session(Tensor tensor, FeedItem[] feed_dict, Graph graph, Session session = null)
        {
            if (session == null)
            {
                session = get_default_session();

                if (session == null)
                    throw new ValueError("Cannot evaluate tensor using `eval()`: No default " +
                           "session is registered. Use `with " +
                           "sess.as_default()` or pass an explicit session to " +
                           "`eval(session=sess)`");

                if (session.graph != graph)
                    throw new ValueError("Cannot use the default session to evaluate tensor: " +
                           "the tensor's graph is different from the session's " +
                           "graph. Pass an explicit session to " +
                           "`eval(session=sess)`.");
            }
            else
            {
                if (session.graph != graph)
                    throw new ValueError("Cannot use the default session to evaluate tensor: " +
                           "the tensor's graph is different from the session's " +
                           "graph. Pass an explicit session to " +
                           "`eval(session=sess)`.");
            }

            return session.run(tensor, feed_dict);
        }

        /// <summary>
        /// Returns the default session for the current thread.
        /// </summary>
        /// <returns>The default `Session` being used in the current thread.</returns>
        public static Session get_default_session()
        {
            return tf.defaultSession;
        }

        /// <summary>
        /// Prepends name scope to a name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="import_scope"></param>
        /// <returns></returns>
        public static string prepend_name_scope(string name, string import_scope)
        {
            if (!string.IsNullOrEmpty(import_scope))
            {
                if (import_scope.EndsWith("/"))
                    import_scope = import_scope.Substring(0, import_scope.Length - 1);

                throw new NotImplementedException("prepend_name_scope");
            }
            else
                return name;
        }

        public static void _run_using_default_session(Operation operation, FeedItem[] feed_dict, Graph graph, Session session)
        {
            if (session == null)
            {
                session = get_default_session();
                if (session == null)
                    throw new ValueError("Cannot execute operation using `run()`: No default " +
                       "session is registered. Use `with " +
                       "sess.as_default():` or pass an explicit session to " +
                       "`run(session=sess)`");
            }

            if (session.graph != graph)
                throw new ValueError("Cannot use the default session to execute operation: " +
                   "the operation's graph is different from the " +
                   "session's graph. Pass an explicit session to " +
                   "run(session=sess).");

            session.run(operation, feed_dict);
        }

        public static Tensor[] convert_n_to_tensor_or_indexed_slices(Tensor[] values, TF_DataType dtype = TF_DataType.DtInvalid, string name = null)
        {
            return internal_convert_n_to_tensor_or_indexed_slices(values, dtype: dtype, name: name);
        }

        public static Tensor convert_to_tensor_or_indexed_slices(Tensor value, TF_DataType dtype = TF_DataType.DtInvalid, string name = null)
        {
            return internal_convert_to_tensor_or_indexed_slices(value: value, dtype: dtype, name: name, as_ref: false);
        }

        public static Tensor internal_convert_to_tensor_or_indexed_slices(Tensor value, TF_DataType dtype = TF_DataType.DtInvalid, string name = null, bool as_ref = false)
        {
            return value;
        }

        public static Tensor[] internal_convert_n_to_tensor_or_indexed_slices(Tensor[] values, TF_DataType dtype = TF_DataType.DtInvalid, string name = null, bool as_ref = false)
        {
            var ret = new List<Tensor>();

            foreach(var (i, value) in Python.enumerate(values))
            {
                if (value == null)
                {
                    ret.Add(value);
                }
                else
                {
                    var n = string.IsNullOrEmpty(name) ? "" : $"{name}_{i}";
                    ret.Add(internal_convert_to_tensor_or_indexed_slices(value, dtype: dtype, name: n, as_ref: as_ref));
                }
            }

            return ret.ToArray();
        }

        public static Tensor[] internal_convert_n_to_tensor(object values, TF_DataType dtype = TF_DataType.DtInvalid, 
            string name = null, TF_DataType preferred_dtype = TF_DataType.DtInvalid, 
            bool as_ref = false)
        {
            var ret = new List<Tensor>();

            foreach((int i, object value) in enumerate(values as object[]))
            {
                string n = string.IsNullOrEmpty(name) ? "" : $"{name}_{i}";
                ret.Add(internal_convert_to_tensor(value, dtype: dtype, name: n, as_ref: as_ref, preferred_dtype: preferred_dtype));
            }

            return ret.ToArray();
        }

        public static Tensor internal_convert_to_tensor(object value, TF_DataType dtype = TF_DataType.DtInvalid,
            string name = null, TF_DataType preferred_dtype = TF_DataType.DtInvalid,
            bool as_ref = false,
            string scope = null)
        {
            if (dtype == TF_DataType.DtInvalid)
                dtype = preferred_dtype;

            switch (value)
            {
                case Tensor tensor:
                    return tensor;
                case Tensor[] tensors:
                    return array_ops._autopacking_helper(tensors, dtype, name);
                case RefVariable varVal:
                    return varVal._TensorConversionFunction(as_ref: as_ref);
                case object[] objects:
                    return array_ops._autopacking_conversion_function(objects, dtype: dtype, name: name);
                default:
                    return constant_op.constant(value, dtype: dtype, name: name);
            }
        }

        public static string strip_name_scope(string name, string export_scope = "")
        {
            if (!string.IsNullOrEmpty(export_scope))
            {
                throw new NotImplementedException("ops.strip_name_scope");
            }
            else
            {
                return name;
            }
        }

        public static string get_name_scope()
        {
            var g = get_default_graph();
            return g.get_name_scope();
        }
    }
}
