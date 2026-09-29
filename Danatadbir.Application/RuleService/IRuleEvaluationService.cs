using Danatadbir.Application.RuleService.Dtos;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Application.RuleService;

public interface IRuleEvaluationService
{
    ReadingEvaluationDto Evaluate(SensorData reading);
}
