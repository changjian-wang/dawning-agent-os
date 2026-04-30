using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Core.Tests;

[TestFixture]
public sealed class ResultTests
{
    [Test]
    public void Success_HasNoErrors()
    {
        var sut = Result.Success();

        Assert.Multiple(() =>
        {
            Assert.That(sut.IsSuccess, Is.True);
            Assert.That(sut.IsFailure, Is.False);
            Assert.That(sut.Errors, Is.Empty);
        });
    }

    [Test]
    public void Failure_FromMultipleErrors_AccumulatesAll()
    {
        var e1 = new DomainError("a", "A");
        var e2 = new DomainError("b", "B", "field2");

        var sut = Result.Failure(e1, e2);

        Assert.Multiple(() =>
        {
            Assert.That(sut.IsSuccess, Is.False);
            Assert.That(sut.IsFailure, Is.True);
            Assert.That(sut.Errors, Has.Length.EqualTo(2));
            Assert.That(sut.Errors[0], Is.EqualTo(e1));
            Assert.That(sut.Errors[1], Is.EqualTo(e2));
        });
    }

    [Test]
    public void Failure_FromCodeAndMessage_BuildsSingleError()
    {
        var sut = Result.Failure("x.y", "message", "field");

        Assert.Multiple(() =>
        {
            Assert.That(sut.Errors, Has.Length.EqualTo(1));
            Assert.That(sut.Errors[0].Code, Is.EqualTo("x.y"));
            Assert.That(sut.Errors[0].Message, Is.EqualTo("message"));
            Assert.That(sut.Errors[0].Field, Is.EqualTo("field"));
        });
    }

    [Test]
    public void Failure_NoErrors_Throws()
    {
        Assert.That(
            () => Result.Failure(),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("errors"));
    }

    [Test]
    public void Failure_NullErrorsArray_Throws()
    {
        Assert.That(
            () => Result.Failure((DomainError[])null!),
            Throws.ArgumentNullException);
    }
}

[TestFixture]
public sealed class ResultOfTTests
{
    [Test]
    public void Success_CarriesValue()
    {
        var sut = Result<int>.Success(42);

        Assert.Multiple(() =>
        {
            Assert.That(sut.IsSuccess, Is.True);
            Assert.That(sut.Value, Is.EqualTo(42));
            Assert.That(sut.Errors, Is.Empty);
        });
    }

    [Test]
    public void Failure_AccessingValue_Throws()
    {
        var sut = Result<int>.Failure("x", "y");

        Assert.That(
            () => _ = sut.Value,
            Throws.InvalidOperationException);
    }

    [Test]
    public void Failure_FromMultipleErrors_AccumulatesAll()
    {
        var e1 = new DomainError("a", "A", "f1");
        var e2 = new DomainError("b", "B", "f2");

        var sut = Result<string>.Failure(e1, e2);

        Assert.Multiple(() =>
        {
            Assert.That(sut.IsFailure, Is.True);
            Assert.That(sut.Errors, Has.Length.EqualTo(2));
            Assert.That(sut.Errors[0], Is.EqualTo(e1));
            Assert.That(sut.Errors[1], Is.EqualTo(e2));
        });
    }

    [Test]
    public void Failure_NoErrors_Throws()
    {
        Assert.That(
            () => Result<int>.Failure(),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("errors"));
    }

    [Test]
    public void Success_WithNullableReferenceValue_AllowsNull()
    {
        // Ensures the API accepts null as a legitimate success value
        // when T is a reference type; consumers decide whether null is meaningful.
        var sut = Result<string?>.Success(null);

        Assert.Multiple(() =>
        {
            Assert.That(sut.IsSuccess, Is.True);
            Assert.That(sut.Value, Is.Null);
        });
    }
}
