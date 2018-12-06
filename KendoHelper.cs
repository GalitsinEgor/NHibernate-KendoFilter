using Kendo.Mvc;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI.Fluent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using VendingWorld.DomainModel.common.exception;
using VendingWorld.DomainModel.common.objects;
using VendingWorld.DomainModel.user.attributes;
using VendingWorld.DomainModel.user.objects;

namespace VendingWorld.DomainModel.common.helpers
{
    public static class KendoHelper
    {
        private const char FilterMemberSplitter = '.';

        private static ConcurrentDictionary<string, string[]> urlAndRequiredPermissions = new ConcurrentDictionary<string, string[]>();
        private static ConcurrentDictionary<string, Assembly> assemblies = new ConcurrentDictionary<string, Assembly>();
        private static Dictionary<FilterOperator, KendoOperatorInfo> kendoOperatorsInfo = new Dictionary<FilterOperator, KendoOperatorInfo>()
        {
            { FilterOperator.IsGreaterThan, new KendoOperatorInfo("больше", "{0} > @0", true) },
            { FilterOperator.IsGreaterThanOrEqualTo, new KendoOperatorInfo("больше или равно", "{0} >= @0", true) },
            { FilterOperator.IsLessThan, new KendoOperatorInfo("меньше", "<", true) },
            { FilterOperator.IsLessThanOrEqualTo, new KendoOperatorInfo("меньше или равно", "{0} <= @0", true) },
            { FilterOperator.IsEqualTo, new KendoOperatorInfo("равно", "{0} = @0", true) },
            { FilterOperator.IsNotEqualTo, new KendoOperatorInfo("не равно", "{0} != @0", true) },
            { FilterOperator.IsNotNull, new KendoOperatorInfo("не null", "{0} != null", false)},
            { FilterOperator.IsNull, new KendoOperatorInfo("null", "{0} == null", false) },
            { FilterOperator.StartsWith, new KendoOperatorInfo("начинается на", "{0}.StartsWith(@0)", true) },
            { FilterOperator.Contains, new KendoOperatorInfo("содержит", "{0}.Contains(@0)", true) },
            { FilterOperator.IsEmpty, new KendoOperatorInfo("пусто", "{0} = \"\"", false) },
            { FilterOperator.IsNotEmpty, new KendoOperatorInfo("не пусто", "{0} != \"\"", false) },
            { FilterOperator.IsNotNullOrEmpty, new KendoOperatorInfo("не null и не пусто", "{0} != "+""+" && {0} != null)", false) },
            { FilterOperator.IsNullOrEmpty, new KendoOperatorInfo("null или пусто", "{0} = \"\" && {0} = null)", false) } 
        };

        public static void SetFilter<T>(GridFilterableSettingsBuilderBase<T> x) where T : GridFilterableSettingsBuilderBase<T>
        {
            x.Operators(y =>
            {
                y.ForDate(z => z.Clear()
                .IsGreaterThan(GetName(FilterOperator.IsGreaterThan))
                .IsGreaterThanOrEqualTo(GetName(FilterOperator.IsGreaterThanOrEqualTo))
                .IsLessThan(GetName(FilterOperator.IsLessThan))
                .IsLessThanOrEqualTo(GetName(FilterOperator.IsLessThanOrEqualTo)));

                y.ForEnums(z => z.Clear()
                .IsEqualTo(GetName(FilterOperator.IsEqualTo))
                .IsNotEqualTo(GetName(FilterOperator.IsNotEqualTo)));

                y.ForString(z => z.Clear()
                .Contains(GetName(FilterOperator.Contains))
                .StartsWith(GetName(FilterOperator.StartsWith))
                .IsEmpty(GetName(FilterOperator.IsEmpty))
                .IsNotEmpty(GetName(FilterOperator.IsNotEmpty))
                //.IsNotNull(GetName(FilterOperator.IsNotNull))
                //.IsNull(GetName(FilterOperator.IsNull))
                //.IsNullOrEmpty(GetName(FilterOperator.IsNullOrEmpty))
                //.IsNotNullOrEmpty(GetName(FilterOperator.IsNotNullOrEmpty))
                );

                y.ForNumber(z => z.Clear()
                .IsGreaterThan(GetName(FilterOperator.IsGreaterThan))
                .IsGreaterThanOrEqualTo(GetName(FilterOperator.IsGreaterThanOrEqualTo))
                .IsLessThan(GetName(FilterOperator.IsLessThan))
                .IsLessThanOrEqualTo(GetName(FilterOperator.IsLessThanOrEqualTo))
                .IsEqualTo(GetName(FilterOperator.IsEqualTo))
                .IsNotEqualTo(GetName(FilterOperator.IsNotEqualTo))
                );
            });
        }
        private static string GetName(FilterOperator key)
        {
            return kendoOperatorsInfo[key].VisibleName;
        }

        public static string DateFormatShort()
        {
            StringBuilder builder = new StringBuilder("{0:");
            builder.Append(CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern);
            builder.Append("}");

            return builder.ToString();
        }

        public static string DateTimeFormatShort()
        {
            StringBuilder builder = new StringBuilder("{0:");
            builder.Append(CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern);
            builder.Append(" ");
            builder.Append(CultureInfo.CurrentUICulture.DateTimeFormat.ShortTimePattern);
            builder.Append("}");

            return builder.ToString();
        }

        public static void InvokeIfPermitted(string assemblyName, string controllersNameSpace, ClaimsPrincipal user, Func<MenuItemBuilder> elementSettings, string method, string controller)
        {
            if (!assemblies.ContainsKey(assemblyName))
            {
                Assembly tmpAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName);
                if(tmpAssembly == null)
                    throw new CommonException($"Can't find an assembly with the name {assemblyName}");

                assemblies.TryAdd(assemblyName, tmpAssembly);
            }

            string url = $"{controller}/{method}".ToLower();

            if(!urlAndRequiredPermissions.ContainsKey(url))
            {
                List<MethodInfo> methods = assemblies[assemblyName].GetType($"{controllersNameSpace}.{controller}Controller")
                    ?.GetMethods().Where(x => x.Name == method).ToList();

                List<string> permissionList = new List<string>();
                foreach(MethodInfo mi in methods)
                {
                    AuthorizeUserAttribute tmpAttr = mi.GetCustomAttribute(typeof(AuthorizeUserAttribute)) as AuthorizeUserAttribute;
                    if(tmpAttr != null && tmpAttr.Arguments != null && tmpAttr.Arguments.Length > 0)
                    {
                        permissionList.AddRange((tmpAttr.Arguments[0] as string).Split(','));
                    }
                }

                if(permissionList.Count > 0)
                {
                    urlAndRequiredPermissions.TryAdd(url, permissionList.Distinct().ToArray());
                }
                else
                {
                    urlAndRequiredPermissions.TryAdd(url, null);
                }
            }

            if(urlAndRequiredPermissions[url] != null)
            {
                IEnumerable<Claim> claims = ClaimHelper.GetUserPermissionClaims(user);
                if (claims.FirstOrDefault(x => x.Value == AuthorizeActionFilter.TotalAccessPermission) == null)
                {
                    foreach (string requiredPermission in urlAndRequiredPermissions[url])
                    {
                        if (claims.FirstOrDefault(x => x.Value == requiredPermission) == null)
                        {
                            return;
                        }
                    }
                }
            }

            elementSettings.Invoke().Action(method, controller);
        }

        public static string MultiSelectCellTemplate(string fieldName, string compareItemsFuncName)
        {
            return $"<div id='#:uid#' class=\"sprite sprite-note clickableCell\">#: template({fieldName},{compareItemsFuncName}) #</div>";
        }

        public static IQueryable<T> NhWhere<T>(this IQueryable<T> query, IList<IFilterDescriptor> filterDescriptors, int offset = 0) where T : class
        {
            IQueryable<T> toReturn = query;
            foreach (IFilterDescriptor iFilter in filterDescriptors)
            {
                List<FilterDescriptor> filters = ConvertToFilterDescriptorList(iFilter);
                foreach (FilterDescriptor filter in filters)
                {
                    KendoOperatorInfo info = kendoOperatorsInfo[filter.Operator];
                    if (info.SetValue)
                    {
                        object value = GetValue<T>(filter, offset);
                        if (value == null)
                            continue;

                        toReturn = toReturn.Where(String.Format(info.LinqOperator, filter.Member), value);
                    }
                    else
                    {
                        toReturn = toReturn.Where(String.Format(info.LinqOperator, filter.Member));
                    }
                }
            }
            return toReturn;
        }
        private static object GetValue<T>(FilterDescriptor filter, int offset)
        {
            Type propertyType = GetFinalType<T>(filter.Member); 
            if (propertyType == null)
                return null;

            if (propertyType.IsEnum)
            {
                return propertyType.GetEnumName(Convert.ToInt32(filter.Value));
            }
            if (propertyType == typeof(DateTime))
            {
                DateTime tmpValue = (DateTime)filter.Value;
                return DateTime.SpecifyKind(tmpValue.AddMinutes(offset), DateTimeKind.Utc);
            }
            if (propertyType == typeof(decimal))
            {
                return Convert.ToDecimal(filter.Value);
            }

            return filter.Value;
        }
        private static Type GetFinalType<TRootObj>(string rawMember)
        {
            Type finalType = typeof(TRootObj);
            string[] members = rawMember.Split(FilterMemberSplitter);
            for (int i = 0; i < members.Length; i++)
            {
                finalType = finalType?.GetProperty(members[i])?.PropertyType;
            }

            return finalType;
        }
        private static List<FilterDescriptor> ConvertToFilterDescriptorList(IFilterDescriptor iFilter)
        {
            List<FilterDescriptor> toReturn = new List<FilterDescriptor>();
            if (iFilter is FilterDescriptor)
            {
                toReturn.Add(iFilter as FilterDescriptor);
            }
            if (iFilter is CompositeFilterDescriptor)
            {
                CompositeFilterDescriptor composite = iFilter as CompositeFilterDescriptor;
                foreach (IFilterDescriptor filter in composite.FilterDescriptors)
                {
                    toReturn.AddRange(ConvertToFilterDescriptorList(filter));
                }
            }

            return toReturn;
        }
    }
}
