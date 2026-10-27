from providers.provider_factory import ProviderFactory


def test_normalize():
    assert ProviderFactory.normalize("OLLAMA") == "ollama"
    assert ProviderFactory.normalize("local") == "ollama"


def test_ollama():
    provider, model = ProviderFactory.describe("ollama")
    assert provider == "ollama"
    assert model == "qwen3:4b-instruct"


def test_unknown_describe():
    provider, model = ProviderFactory.describe("disabled-provider")
    assert provider is None
    assert model is None


def test_unknown_create():
    try:
        ProviderFactory.create("disabled-provider")
    except ValueError:
        return

    raise AssertionError("Only Ollama may be available")
