﻿using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using MoqComplete.Services;
using System.Linq;

namespace MoqComplete.CodeAnalysis
{
    [ElementProblemAnalyzer(typeof(IInvocationExpression), HighlightingTypes = new[] { typeof(SuspiciousCallbackWarning) })]
    public class SupiciousCallbackAnalyzer : ElementProblemAnalyzer<IInvocationExpression>
    {
        protected override void Run(IInvocationExpression element, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            var methodIdentitifer = element.GetSolution().GetComponent<IMoqMethodIdentifier>();
            var mockedMethodProvider = element.GetSolution().GetComponent<IMockedMethodProvider>();

            if (!methodIdentitifer.IsMoqCallbackMethod(element))
                return;

            var typeParameters = element.TypeArguments;
            if (typeParameters.Count == 0)
                return;

            var pointer = element.InvokedExpression;
            IMethod mockedMethod = null;

            while (pointer != null && mockedMethod == null && pointer.FirstChild is IInvocationExpression methodInvocation)
            {
                mockedMethod = mockedMethodProvider.GetMockedMethodFromSetupMethod(methodInvocation);
                pointer = methodInvocation.InvokedExpression;
            }

            if (mockedMethod == null)
                return;

            var expectedTypesParameter = mockedMethod.Parameters.Select(x => x.Type).ToArray();

            if (expectedTypesParameter.Length <= 0)
                return;

            if (typeParameters.Count != expectedTypesParameter.Length)
                AddWarning(element, consumer);
            else
            {
                for (int i = 0; i < typeParameters.Count; i++)
                {
                    if (!typeParameters[i].Equals(expectedTypesParameter[i]))
                        AddWarning(element, consumer);
                }
            }
        }

        private static void AddWarning(IInvocationExpression element, IHighlightingConsumer consumer)
        {
            DocumentRange range;
            if (element.FirstChild?.LastChild is ITypeArgumentList typeInvocation)
                range = typeInvocation.GetDocumentRange();
            else
                range = element.InvokedExpression.GetDocumentRange();

            consumer.AddHighlighting(new SuspiciousCallbackWarning(element, range));
        }
    }
}
