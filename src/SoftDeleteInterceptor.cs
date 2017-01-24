namespace EFAutoSoftDelete
{
  public class SoftDeleteInterceptor : IDbCommandTreeInterceptor
  {
    public const string IsDeletedColumnName = "IsDeleted";

    public void TreeCreated(DbCommandTreeInterceptionContext interceptionContext)
    {
      if (interceptionContext.OriginalResult.DataSpace != DataSpace.SSpace)
      {
        return;
      }

      var queryCommand = interceptionContext.Result as DbQueryCommandTree;
      if (queryCommand != null)
      {
        interceptionContext.Result = HandleQueryCommand(queryCommand);
      }

      var deleteCommand = interceptionContext.OriginalResult as DbDeleteCommandTree;
      if (deleteCommand != null)
      {
        interceptionContext.Result = HandleDeleteCommand(deleteCommand);
      }
    }

    private static DbCommandTree HandleDeleteCommand(DbDeleteCommandTree deleteCommand)
    {
      var setClauses = new List<DbModificationClause>();
      var table = (EntityType) deleteCommand.Target.VariableType.EdmType;

      if (table.Properties.All(p => p.Name != IsDeletedColumnName))
      {
        return deleteCommand;
      }

      var varName = deleteCommand.Target.VariableName;
      var variable = deleteCommand.Target.VariableType.Variable(varName);
      var property = variable.Property(IsDeletedColumnName);
      var value = DbExpression.FromBoolean(true);
      setClauses.Add(DbExpressionBuilder.SetClause(property, value));

      return new DbUpdateCommandTree(deleteCommand.MetadataWorkspace,
                                     deleteCommand.DataSpace,
                                     deleteCommand.Target,
                                     deleteCommand.Predicate,
                                     setClauses.AsReadOnly(), null);
    }

    private static DbCommandTree HandleQueryCommand(DbQueryCommandTree queryCommand)
    {
      var newQuery = queryCommand.Query.Accept(new SoftDeleteQueryVisitor());

      return new DbQueryCommandTree(queryCommand.MetadataWorkspace,
                                    queryCommand.DataSpace, newQuery);
    }

    public class SoftDeleteQueryVisitor : DefaultExpressionVisitor
    {
      public override DbExpression Visit(DbScanExpression expression)
      {
        var table = (EntityType)expression.Target.ElementType;
        if (table.Properties.All(p => p.Name != IsDeletedColumnName))
        {
          return base.Visit(expression);
        }

        var binding = expression.Bind();

        return binding.Filter(binding.VariableType
                                     .Variable(binding.VariableName)
                                     .Property(IsDeletedColumnName)
                                     .NotEqual(DbExpression.FromBoolean(true)));
      }
    }
  }
}
