# Dead Man's Credit

**Type:** Outlaw bounty chain (4 stages)
**Scale:** Small, personal. No conspiracy, no warlord.
**Target of record:** Halden Vos
**Target in fact:** Sera Idowu
**Bounty issuer:** Trackers Alliance, on behalf of GalBank
**Canon anchors:** Trackers Alliance, GalBank, Ecliptic Mercenaries, Freestar residency, The Den, post-Armistice displacement (Colony War ended 2311; now 2330)
**Invented:** one practice (identity tenancy), one venue (the deck-four salvage line on The Den), three people. Nothing that contradicts canon.

**Premise, one line:** The man you are hunting has been dead for three years, and eleven people have used his name to get through doors that were shut to them.

---

## Author's note (delete before conversion)

The four stages each **reverse** the previous one rather than widening it:

1. Your target is a debt-skip. → He is dead, and has been for three years.
2. He is dead. → Someone is maintaining the corpse's paperwork on purpose.
3. It is maintained. → It is *rented*, cheaply, to people with no papers, by a woman who is not hiding it.
4. It is a mercy. → The current tenant is an Ecliptic contractor with a real bill outstanding, and she is not the sort of person the door was built for.

No stage restates a previous one. The player's belief about what they are doing is wrong three times.

**The player deduces the central fact rather than being told it.** The Discovery dataslate contains two documents and no commentary. The bounty writ in the player's own inventory carries Vos's chip serial. The clinic disposal invoice carries the same serial. Nobody says "he is dead." The player reads two numbers.

**The characterisation is load-bearing, not decorative.** Sera's Ecliptic history is not colour: it is the reason she cannot use her own name, the reason she can afford papers she did not need to rent, and the reason the ending is not a clean act of kindness.

---

## LoreFile

```xml
<LoreFile>

    <Summary>
        Halden Vos was a HopeTech freight loader who ran up eleven thousand credits of GalBank
        arrears and skipped to The Den in 2325. He died there of cardiac failure in 2327, on
        the table of a station clinic, and was cremated under the name on his chip. His debt
        never noticed. His Freestar residency, his GalBank line and his work permit are all
        still current, and for three years the clinic administrator who signed his disposal
        order has been renting them out.
    </Summary>

    <TargetProfile>
        - The name, not the man, is the asset. Vos left behind a clean residency, an active
          credit line, and a work permit with no flags. On The Den these are worth more than
          most people's lives.
        - Eleven tenants so far. Two have since died. One went home. The rest are somewhere
          being Halden Vos.
        - The maintenance is mundane and visible: a quarterly authorisation form, a forwarding
          arrangement, rent paid three days early from a GalBank draft. It survives because it
          is boring, not because it is hidden.
        - The current tenant is Sera Idowu, eight months in. Ecliptic contractor, 2318 to 2329.
          Her own file follows her; Ecliptic do not close a contract, they suspend it. She paid
          Rask the standard renewal fee and nothing over, and she did not need to.
    </TargetProfile>

    <Motives>
        - Rask wants the arrangement to keep being boring. She takes no profit beyond cost and
          keeps no list, which is the only reason it has lasted.
        - Sera wants to stop being findable. She is not repentant and does not claim to be.
        - Neither of them wants Vos found. A closed file kills the door.
    </Motives>

    <Faction>
        The Trackers Alliance holds the writ and does not care who satisfies it. GalBank wants
        the arrears or a death certificate, whichever arrives first, and has wanted either for
        five years without spending a credit to find out. Ecliptic Mercenaries have an open
        internal file on Sera Idowu and no legal standing on The Den, which is why she is there.
    </Faction>

    <GeographicContext>
        The Den is neutral, Trade Authority run, and lightly policed. Deck two holds the water
        plant and the clinic. Deck four is the salvage line, where hulls come apart and nobody
        asks for papers to start a shift, only to end one.
    </GeographicContext>

</LoreFile>
```

---

## PlannedArc

```xml
<PlannedArc>
    <Discovery>
        <Theme>A cold five-year debt-skip writ leads to a rented hab whose tenant has paid early, in full, every quarter for three years, and left behind a slate carrying two documents that cannot both be true.</Theme>
        <Template>Dataslate in levelled item</Template>
    </Discovery>
    <Investigation>
        <Stage>
            <Theme>The dock clerk who processes the rent has never once laid eyes on the tenant, and the standing authorisation that forwards his mail has been signed in two different hands.</Theme>
            <Template>Station Conversation - The Den Dock Clerk</Template>
        </Stage>
        <Stage>
            <Theme>The clinic administrator does not deny it, explains what the name is for, and gives up the current tenant without being pressed, because she has decided the arrangement survives the loss of one.</Theme>
            <Template>Station Conversation - The Den Clinic</Template>
        </Stage>
    </Investigation>
    <Showdown>
        <Theme>Sera Idowu is not hiding on the salvage line and will not run, and the player learns that the writ can be closed three ways, only one of which pays.</Theme>
        <Template>Station Bounty - The Den Deck Four</Template>
    </Showdown>
</PlannedArc>
```

---

## Stage 1 — Discovery

**Location:** The Den, hab block C, unit 14
**Template:** Dataslate in levelled item
**Player believes on entry:** Halden Vos is a debt-skip hiding on a neutral station.
**Player believes on exit:** Halden Vos is dead, and someone is paying his rent.

### Log entry

> Halden Vos, HopeTech freight loader, skipped eleven thousand in GalBank arrears and was last
> filed on The Den. Trackers Alliance writ is five years cold and still open. His rented hab on
> block C is paid current. Start there. A tenant who pays on time for five years is either not
> hiding or not the one paying.

### Asset: BOOK — `dmc_slate_habrecords`

Presented as a maintenance slate left in the unit. Two documents, no commentary, no summary screen.

```
GALBANK ARREARS NOTICE
Account holder: VOS, HALDEN R.
Chip serial: 4471-K
Principal outstanding: 11,204 cr
Notice issued: 12.06.2330
Status: ACTIVE. Referred to Trackers Alliance.

---

THE DEN STATION CLINIC - DISPOSAL AUTHORISATION
Decedent chip serial: 4471-K
Cause: cardiac arrest, unattended
Received: 03.11.2327
Disposal: cremation, station protocol
Authorising officer: T. RASK
Next of kin: none listed
Effects: none claimed
```

> **Convert note:** the player's Trackers Alliance writ (existing bounty item) must display the
> chip serial `4471-K`. That single number is the entire deduction. Do not add a journal entry
> that states the target is dead. If the player does not notice, the dock clerk in Stage 2 says
> a line that makes it land.

### Objective

`Search the rented hab on The Den` → `Recover the maintenance slate`

---

## Stage 2 — Investigation 1

**Location:** The Den, dock office
**Template:** Station Conversation - The Den Dock Clerk
**NPC:** Corrin Ashe, male, dock clerk, Trade Authority contractor
**Player believes on entry:** Vos is dead and someone is paying his rent.
**Player believes on exit:** The name is being *maintained*, deliberately, like a piece of equipment.
**Intrigue detail:** the same form, the same signature, two different hands.

### Log entry

> The hab slate puts a clinic disposal order and a live arrears notice on the same chip serial.
> The dock office processes the rent. Corrin Ashe works the desk and handles the block C ledger.
> Ask who signs, who collects, and whether anyone has ever seen the tenant in person.

### Asset: DIAL — `dmc_dial_ashe`

```
GREETING: Scanner's been flagging your ship since the Wolf jump. You want the fee waived or itemised?
PLAYER1: Who paid rent on hab fourteen last quarter?
NPC1a: Vos. Halden Vos. Same as the eleven quarters before it. GalBank draft, clears clean, always three days early.
NPC1b: Tidiest account on this station and I have never once had to chase him. I would take a hundred more like him.
PLAYER2: Who collects his mail?
NPC2a: Nobody collects. It gets forwarded. Rask's clinic on deck two takes delivery on his behalf, standing authorisation.
NPC2b: Same form every quarter. Same signature. Different handwriting twice that I noticed.
PLAYER3: Where's the clinic?
NPC3a: Deck two, aft of the water plant. Ask for Teodora Rask. She runs the desk herself and keeps the files behind it.
NPC3b: Go before second shift. After that she stops answering the door.
```

> **Convert note:** all lines within limits (GREETING 90, PLAYER max 42, NPC max 146). No em dashes.
> Ashe knows only what a dock clerk sees: a ledger and a form. He does not know Vos is dead, and
> his line about wanting a hundred more like him is the joke he cannot hear.

### Objective

`Speak to Corrin Ashe at the dock office` → `Go to the clinic on deck two`

---

## Stage 3 — Investigation 2

**Location:** The Den, deck two clinic
**Template:** Station Conversation - The Den Clinic
**NPC:** Teodora Rask, female, clinic administrator
**Player believes on entry:** Someone is running a fraud with a dead man's papers.
**Player believes on exit:** It is a tenancy, it is cheap, it has eleven names behind it, and the woman running it will hand over the current one without being threatened.

### Log entry

> Teodora Rask signed the disposal order on Halden Vos in 2327 and has taken delivery of his
> mail every quarter since. The dock clerk has never seen the tenant. Rask has. Find out what
> she is keeping the name alive for, and who is wearing it now.

### Asset: DIAL — `dmc_dial_rask`

```
GREETING: Sit or don't, but shut the door behind you. The seal is poor and I pay to heat this room.
PLAYER1: Why do you take Halden Vos's mail?
NPC1a: Because he died on my table. Cardiac, unattended, three years back. I filed him under the name on his chip.
NPC1b: The paperwork never noticed. Freestar residency, GalBank line, work permit. All of it still breathing.
PLAYER2: Who is using it?
NPC2a: Whoever needs a door. They come to me, I renew the authorisation, they pay what the renewal costs and not one credit over.
NPC2b: Eleven so far. Two are dead now. One went home. The rest are somewhere being Halden Vos.
PLAYER3: Who has it now?
NPC3a: A woman came in eight months back with Ecliptic scarring up her forearm and enough credits that she did not need me. She took it anyway.
NPC3b: Deck four, the salvage line, night shift. Go down without a weapon showing.
```

> **Convert note:** Rask gives up Sera without being pressed. This is not weakness and should not
> be played as fear. She has decided the arrangement outlives any one tenant, and a hunter who
> leaves satisfied does not come back with a writ for her. If the player asks why she gave up a
> tenant, the barter line is `dmc_dial_rask_why`:
>
> `NPC: One name is not the door. I have signed four hundred disposal orders. Come back in a year.`

### Objective

`Speak to Teodora Rask` → `Find the woman on deck four`

---

## Stage 4 — Showdown

**Location:** The Den, deck four salvage line
**Template:** Station Bounty - The Den Deck Four
**NPC:** Sera Idowu, female, hostile-capable but does not open hostile
**Player believes on entry:** He is collecting a bounty from a woman using a dead man's name.
**Player learns:** the writ is against the name, and the name is a door eleven people have walked through.

### Log entry

> Sera Idowu holds the Vos identity and works the deck four salvage line. Ecliptic have an open
> internal file on her and no standing on The Den. The Trackers Alliance writ names Halden Vos,
> not Idowu. Decide what satisfies it.

### Asset: DIAL — `dmc_dial_sera`

```
GREETING: Torch is hot, so stand back a step. If you have come to hire, I finish at four.
PLAYER1: Halden Vos.
NPC1a: That is the name on my permit. It is not a name I picked and it is not one I would have picked.
NPC1b: You will have been to Rask. She tells everyone eventually. I have never once held that against her.
PLAYER2: Ecliptic.
NPC2a: Eleven years. They do not release you, they suspend you, and a suspended file travels better than a live one.
NPC2b: I did not buy the name because I could not afford my own. I bought it because mine is worth reading.
PLAYER3: Give me one reason not to take you in.
NPC3a: I cannot give you one. Take me in and the writ closes, the name gets flagged, and Rask's door shuts behind me.
NPC3b: There are three people wearing worse histories than mine who have not got out yet. That is not a reason. That is just what happens next.
```

### The three closes

The writ says **Halden Vos**. It does not say Sera Idowu. All three satisfy it in the fiction; only one pays.

| Close | How | Pays | Consequence |
|---|---|---|---|
| **Take her in** | Kill or capture Sera | Full writ, 4,000 cr | Vos flagged. Rask's arrangement ends. Ashe's ledger goes quiet. |
| **File him dead** | Return the disposal order to any Trackers Alliance kiosk | Nothing. Writ voids on a death certificate. | Vos closes clean. Sera loses the papers and stays free. Rask has to find another decedent. |
| **Leave it open** | Walk out | Nothing | Everything continues, including the writ, which stays on the board for the next hunter. |

> **Convert note:** the second close is the one the record system has to be taught. It requires
> the disposal order (`dmc_slate_habrecords`) to be a turn-in item at a Trackers Alliance
> terminal, and it must void the bounty rather than complete it. If that is expensive, cut the
> third close before you cut the second. The third is a mood; the second is the point.

---

## Convertible index

| Record | ID | Notes |
|---|---|---|
| QUST | `dmc_quest_deadmanscredit` | 4 stages, 3 close branches |
| BOOK | `dmc_slate_habrecords` | two documents, no commentary; turn-in item for close 2 |
| NPC_ | `dmc_npc_ashe` | male, dock clerk, The Den dock office |
| NPC_ | `dmc_npc_rask` | female, clinic administrator, deck two |
| NPC_ | `dmc_npc_sera` | female, salvage worker, deck four, hostile-capable |
| DIAL | `dmc_dial_ashe` | 10 lines |
| DIAL | `dmc_dial_rask` | 10 lines + 1 barter line |
| DIAL | `dmc_dial_sera` | 10 lines |
| MESG | `dmc_msg_writvoid` | shown on close 2 |
| Bounty writ | (existing) | must surface chip serial `4471-K` |

**House rules honoured:** no em dashes in any in-game string; no NPC opens by naming the target or
predicting the player's question; each dialogue beat carries exactly one class of information
(observation / logistics / location); no log entry begins with a label; every log is stage-locked
to what the player could know at that point.
