using FinanceTracker.Domain.Classifications;

namespace FinanceTracker.Tests.Unit.Domain.Classifications;

[Category("Domain")]
public class CustomClassificationTests
{

    [Test]
    public async Task CustomClassification_ValidConstruction_SetsProperties()
    {
        var userId = Guid.NewGuid();

        var classification = new CustomClassification(userId, "Groceries");

        using (Assert.Multiple())
        {
            await Assert.That(classification.UserId).IsEqualTo(userId);
            await Assert.That(classification.Tag).IsEqualTo("Groceries");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public void CustomClassification_InvalidTag_Throws_ArgumentException(string? tag)
    {
        Assert.Throws<ArgumentException>(() =>
            _ = new CustomClassification(Guid.NewGuid(), tag!));
    }

    [Test]
    public void CustomClassification_EmptyUserId_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new CustomClassification(Guid.Empty, "Groceries"));
    }

    [Test]
    public async Task CustomClassification_Rename_ValidString_UpdatesTag()
    {
        var classification = new CustomClassification(Guid.NewGuid(), "Groceries");

        classification.Rename("Food");

        await Assert.That(classification.Tag).IsEqualTo("Food");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public void CustomClassification_Rename_Invalid_Throws_ArgumentException(string? tag)
    {
        var classification = new CustomClassification(Guid.NewGuid(), "Groceries");

        Assert.Throws<ArgumentException>(() =>
            classification.Rename(tag!));
    }
}
