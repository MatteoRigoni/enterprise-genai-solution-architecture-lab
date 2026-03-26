using AiSa.Domain.Eval;

namespace AiSa.Application.Eval;

public interface IEvalService
{
    EvalMetrics ComputeMetrics(IReadOnlyList<EvalResult> results);
}

