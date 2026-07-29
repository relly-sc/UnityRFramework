using System;
using System.Collections.Generic;
using System.Reflection;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 流程组件。作为 ProcedureModule 的运行时包装层，
    /// 绑定 Unity 生命周期，暴露 Inspector 统计信息，转发所有流程操作到 ProcedureModule。
    /// </summary>
    /// <remarks>
    /// 使用方式：在游戏启动脚本中按程序集自动注册或手动注册流程状态，
    /// 再调用 StartProcedure&lt;T&gt;() 启动。
    /// <code>
    /// // 自动发现 ProcedureLaunch 所在程序集中的全部流程状态。
    /// GameEntry.Procedure.InitializeFromAssembly&lt;ProcedureLaunch&gt;();
    /// GameEntry.Procedure.StartProcedure&lt;ProcedureLaunch&gt;();
    /// </code>
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Procedure")]
    [DisallowMultipleComponent]
    public sealed class ProcedureComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 流程模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IProcedureModule procedureModule;

        /// <summary>
        /// 获取跨状态数据黑板。
        /// </summary>
        public ProcedureBlackboard Blackboard
        {
            get { return procedureModule != null ? procedureModule.Blackboard : null; }
        }

        /// <summary>
        /// 获取当前运行中的流程状态实例（运行时只读）。
        /// </summary>
        public ProcedureStateBase CurrentProcedure
        {
            get { return procedureModule != null ? procedureModule.CurrentProcedure : null; }
        }

        /// <summary>
        /// 获取当前流程状态已运行的持续时间（秒）（运行时只读）。
        /// </summary>
        public float CurrentProcedureTime
        {
            get { return procedureModule != null ? procedureModule.CurrentProcedureTime : 0f; }
        }

        /// <summary>
        /// 获取已注册流程状态数量。
        /// </summary>
        public int ProcedureCount
        {
            get { return procedureModule != null ? procedureModule.ProcedureCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();
            procedureModule = RFrameworkModuleEntry.GetModule<IProcedureModule>();
        }

        /// <inheritdoc cref="IProcedureModule.Initialize"/>
        public void Initialize(params ProcedureStateBase[] procedures)
        {
            procedureModule.Initialize(procedures);
        }

        /// <summary>
        /// 自动发现并注册指定流程状态所在程序集中的全部流程状态。
        /// </summary>
        /// <typeparam name="TAssemblyMarker">用于定位业务程序集的任意流程状态类型。</typeparam>
        /// <remarks>
        /// 被发现的状态必须是非抽象、非泛型类型，并提供公共无参构造函数。
        /// 使用自定义程序集定义文件且以 IL2CPP 发布时，应通过 link.xml 保留对应业务程序集。
        /// </remarks>
        public void InitializeFromAssembly<TAssemblyMarker>() where TAssemblyMarker : ProcedureStateBase
        {
            InitializeFromAssemblies(typeof(TAssemblyMarker).Assembly);
        }

        /// <summary>
        /// 自动发现并注册指定程序集中的全部流程状态。
        /// </summary>
        /// <param name="assemblies">要扫描的业务程序集。</param>
        /// <remarks>
        /// 仅扫描显式传入的程序集，不扫描整个应用程序域，避免误注册其他示例、测试或插件状态。
        /// </remarks>
        public void InitializeFromAssemblies(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length < 1)
            {
                throw new RFrameworkException("Procedure assemblies are invalid.");
            }

            HashSet<Type> discoveredTypes = new HashSet<Type>();
            List<Type> procedureTypes = new List<Type>();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    throw new RFrameworkException("Procedure assembly is invalid.");
                }

                Type[] types = GetAssemblyTypes(assembly);
                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null || type.IsAbstract || type.ContainsGenericParameters ||
                        !typeof(ProcedureStateBase).IsAssignableFrom(type) || !discoveredTypes.Add(type))
                    {
                        continue;
                    }

                    if (type.GetConstructor(Type.EmptyTypes) == null)
                    {
                        throw new RFrameworkException(Utility.Text.Format(
                            "Procedure state '{0}' must provide a public parameterless constructor.",
                            type.FullName));
                    }

                    procedureTypes.Add(type);
                }
            }

            if (procedureTypes.Count < 1)
            {
                throw new RFrameworkException("No concrete procedure states were found in the specified assemblies.");
            }

            procedureTypes.Sort(delegate(Type left, Type right)
            {
                return string.CompareOrdinal(left.FullName, right.FullName);
            });

            ProcedureStateBase[] procedures = new ProcedureStateBase[procedureTypes.Count];
            for (int i = 0; i < procedureTypes.Count; i++)
            {
                Type procedureType = procedureTypes[i];
                try
                {
                    procedures[i] = (ProcedureStateBase)Activator.CreateInstance(procedureType);
                }
                catch (Exception exception)
                {
                    throw new RFrameworkException(Utility.Text.Format(
                        "Can not create procedure state '{0}'.", procedureType.FullName), exception);
                }
            }

            procedureModule.Initialize(procedures);
        }

        /// <summary>
        /// 获取程序集中的全部类型；加载失败时终止自动注册，避免得到不完整的流程状态集合。
        /// </summary>
        private static Type[] GetAssemblyTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                Exception innerException = exception.LoaderExceptions != null && exception.LoaderExceptions.Length > 0
                    ? exception.LoaderExceptions[0]
                    : exception;
                throw new RFrameworkException(Utility.Text.Format(
                    "Can not load all types from procedure assembly '{0}'.", assembly.FullName), innerException);
            }
            catch (Exception exception)
            {
                throw new RFrameworkException(Utility.Text.Format(
                    "Can not inspect procedure assembly '{0}'.", assembly.FullName), exception);
            }
        }

        /// <inheritdoc cref="IProcedureModule.StartProcedure{T}"/>
        public void StartProcedure<T>() where T : ProcedureStateBase
        {
            procedureModule.StartProcedure<T>();
        }

        /// <inheritdoc cref="IProcedureModule.ChangeProcedure{T}"/>
        public void ChangeProcedure<T>() where T : ProcedureStateBase
        {
            procedureModule.ChangeProcedure<T>();
        }

        /// <inheritdoc cref="IProcedureModule.GetProcedure{T}"/>
        public T GetProcedure<T>() where T : ProcedureStateBase
        {
            return procedureModule.GetProcedure<T>();
        }

        /// <inheritdoc cref="IProcedureModule.HasProcedure{T}"/>
        public bool HasProcedure<T>() where T : ProcedureStateBase
        {
            return procedureModule.HasProcedure<T>();
        }
    }
}
