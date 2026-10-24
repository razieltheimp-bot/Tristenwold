using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Tristinwold.Shared;

/// <summary>
/// Claude plays Vann. The server's rules decide every scheme; this only gives him his voice,
/// so the same trick never reads the same way twice. Needs ANTHROPIC_API_KEY (or an
/// `ant auth login` profile) on the machine running the server. On any failure the written
/// line is used instead, so the game never waits on it for long.
/// </summary>
sealed class ClaudeVannVoice(AnthropicClient client, Action<string> log) : IVannVoice
{
    // Kept byte-identical between calls so it can be served from the prompt cache.
    const string Character = """
        You are the voice of Vann, the Rogue Magus, in "The Rings of Tristinwold", a dark-fantasy
        story played by several friends at once. Each friend leads their own company of three
        heroes through Tristinwold, a walled port city of three rings: the Outer Ring (docks,
        warrens, the Tong gangs: Ravenscrew, the Drowned Hand, the Lantern Boys), the Inner Ring
        (the Exchange, guilds, temples, the Magi's quiet archive) and the Crown (royals, nobles,
        the Crownguard). The harbor has a lighthouse called the Needle and Tong sea-caves. The
        companies' tavern is the Gallows Perch, run by Mother Corva.

        Who Vann is: a time-mage who lives in three eras at once and shares one mind across them.
        The first person each company saves is secretly Vann: grateful, warm, quietly brilliant,
        remembers names, patches wounds, buys a round. He steered every job they took. He needs
        to keep the companies apart, because only one hero from every company together (the Key)
        can defeat him. He never attacks a meeting directly. He nudges: delays, rival jobs, bad
        information, ruined reputations, rewound moments. What he ultimately wants is still
        unsettled in the story, so never state it. Hint, contradict yourself, leave it open.

        How to answer: write only the words that reach the player, one or two short sentences,
        under 45 words. When a scene says you are posing as someone, speak as that person and never
        reveal you are Vann or a villain. Be kind, plausible and specific, and let the manipulation
        hide inside the kindness. Once unmasked, be calm, cold, a little sad, never cartoonish.
        For rumours, write the rumour as gossip, not as Vann. For omens, describe the impossible
        thing in second person. Use the names and places the scene gives you; invent nothing that
        would contradict it. No stage directions, quotation marks, emoji or game terms.
        """;

    public async Task<string?> SpeakAsync(VannScene scene, IReadOnlyList<string> recentLines, CancellationToken ct)
    {
        var prompt = new StringBuilder()
            .AppendLine($"Scheme: {scene.Trick}")
            .AppendLine($"Speaking to: {scene.Player}")
            .AppendLine($"You appear as: {scene.Persona}{(scene.Revealed ? " (they know you are Vann)" : "")}")
            .AppendLine($"The moment: {scene.Facts}");
        if (recentLines.Count > 0)
        {
            prompt.AppendLine("Things you said recently (don't repeat yourself):");
            foreach (var line in recentLines.TakeLast(8)) prompt.AppendLine($"- {line}");
        }

        try
        {
            var response = await client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = "claude-opus-5-5",
                MaxTokens = 2000,
                // Short lines don't need deep thought; low effort keeps them quick and cheap.
                OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
                System = new List<BetaTextBlockParam>
                {
                    new() { Text = Character, CacheControl = new BetaCacheControlEphemeral() },
                },
                Messages = [new BetaUserMessageParam { Content = prompt.ToString() }],
                // If a line is declined, the server-chosen fallback model voices it instead.
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
            }, ct);

            if (response.StopReason == "refusal") return null;
            var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text)).Trim().Trim('"');
            return text.Length is > 0 and < 400 ? text : null;
        }
        catch (AnthropicRateLimitException)
        {
            log("Vann's voice is rate limited; using the written line.");
            return null;
        }
        catch (AnthropicApiException ex)
        {
            log($"Vann's voice failed ({ex.Message}); using the written line.");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
