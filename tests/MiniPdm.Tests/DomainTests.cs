using Xunit;
using MiniPdm.Application.Services;
using MiniPdm.Domain.Enums;

namespace MiniPdm.Tests;

public class DomainTests
{
    [Theory]
    [InlineData("АБВГ.301245.001")]
    [InlineData("РДЦЛ.304112.300")]
    public void Designation_ShouldBeValid(string value)
    {
        Assert.True(DomainRules.IsValidDesignation(value));
    }

    [Theory]
    [InlineData("PДЦЛ.304112.601")]
    [InlineData("АБВГ.30124.001")]
    [InlineData("АБВГ-301245-001")]
    public void Designation_ShouldBeInvalid(string value)
    {
        Assert.False(DomainRules.IsValidDesignation(value));
    }

    [Theory]
    [InlineData(ObjectState.InWork, ObjectState.Approved, true)]
    [InlineData(ObjectState.InWork, ObjectState.Cancelled, true)]
    [InlineData(ObjectState.Approved, ObjectState.Cancelled, true)]
    [InlineData(ObjectState.Approved, ObjectState.InWork, false)]
    [InlineData(ObjectState.Cancelled, ObjectState.InWork, false)]
    public void StateTransition_ShouldFollowSpecification(ObjectState from, ObjectState to, bool expected)
    {
        Assert.Equal(expected, DomainRules.CanChangeState(from, to));
    }
}
