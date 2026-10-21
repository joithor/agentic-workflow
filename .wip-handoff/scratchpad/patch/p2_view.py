root='/home/thor/repos/aw-wt/sindri-ui-needs-you-2/src/Sindri/Aw.Sindri.Ui/'
p=root+'Views/NeedsYou/NeedsYouPage.axaml'
s=open(p).read()

# 1. the detail column becomes a grid: the card above, the bar under it
old_open='''      <Panel x:Name="NeedsYouDetail" Grid.Column="1" Classes="detail-canvas">
        <TextBlock x:Name="NeedsYouInboxEmpty"'''
new_open='''      <Grid x:Name="NeedsYouDetail" Grid.Column="1" Classes="detail-canvas" RowDefinitions="*,Auto">
       <Panel>
        <TextBlock x:Name="NeedsYouInboxEmpty"'''
assert old_open in s
s=s.replace(old_open,new_open)

s=s.replace('<ScrollViewer x:Name="NeedsYouCardScroll" IsVisible="{Binding HasSelected}" HorizontalScrollBarVisibility="Disabled">',
            '<ScrollViewer x:Name="NeedsYouCardScroll" IsVisible="{Binding HasSelected}" HorizontalScrollBarVisibility="Disabled" ScrollChanged="OnCardScrolled">\n          <Panel>')
s=s.replace('<Border x:Name="NeedsYouCard" Classes="request-card"','<Border x:Name="NeedsYouCard" Classes="request-card" IsVisible="{Binding ShowsRequest}"')

# 2. the More menu in the card head, next to the kind word
old_kind='''                <TextBlock x:Name="NeedsYouCardKind" Grid.Column="2" Classes="eyebrow attention" Text="{Binding KindWord}" VerticalAlignment="Top" AutomationProperties.AccessibilityView="Raw" />'''
new_kind='''                <StackPanel Grid.Column="2" Spacing="6" HorizontalAlignment="Right">
                  <TextBlock x:Name="NeedsYouCardKind" Classes="eyebrow attention" Text="{Binding KindWord}" HorizontalAlignment="Right" AutomationProperties.AccessibilityView="Raw" />
                  <Button x:Name="NeedsYouMore" Classes="copy" HorizontalAlignment="Right" IsVisible="{Binding Bar.ShowsDrop}" AutomationProperties.Name="{Binding Bar.MoreName}">
                    <Button.Flyout>
                      <MenuFlyout Placement="BottomEdgeAlignedRight">
                        <MenuItem x:Name="NeedsYouDropItem" Click="OnDropItem" />
                      </MenuFlyout>
                    </Button.Flyout>
                    <TextBlock Classes="caption" Text="{Binding Bar.MoreWord}" AutomationProperties.AccessibilityView="Raw" />
                  </Button>
                </StackPanel>'''
assert old_kind in s
s=s.replace(old_kind,new_kind)

# 3. the row's own controls only where no bar takes the request
s=s.replace('<ItemsControl x:Name="NeedsYouCardControls" ItemsSource="{Binding Card.Controls}" IsEnabled="{Binding !IsCleared}" Margin="20,8">',
            '<ItemsControl x:Name="NeedsYouCardControls" ItemsSource="{Binding Card.Controls}" IsEnabled="{Binding !IsCleared}" IsVisible="{Binding Bar.ShowsRowControls}" Margin="20,8">')
s=s.replace("<!-- Until the action bar (slice 2) the card carries the row's own controls, as Home's row does: a link to a row still answers it. -->",
            "<!-- A failure, and a paused run or settings request the bar has no primary for, keep the row's own controls, as Home's row does: a link to the row still answers it. -->")

# 4. close the card panel with the done card, then the bar
old_close='''            </StackPanel>
          </Border>
        </ScrollViewer>
      </Panel>
    </Grid>'''
new_close='''            </StackPanel>
          </Border>
          <Border x:Name="NeedsYouDone" Classes="request-card" IsVisible="{Binding IsDone}" AutomationProperties.Name="{Binding Bar.DoneTitle}" AutomationProperties.AccessibilityView="Control" AutomationProperties.ControlTypeOverride="Group">
            <StackPanel Spacing="4" Margin="20,24">
              <TextBlock x:Name="NeedsYouDoneTitle" Classes="detail-title" Text="{Binding Bar.DoneTitle}" TextWrapping="Wrap" AutomationProperties.LiveSetting="Polite" />
              <TextBlock x:Name="NeedsYouDoneBody" Classes="body muted" Text="{Binding Bar.DoneBody}" TextWrapping="Wrap" />
            </StackPanel>
          </Border>
          </Panel>
        </ScrollViewer>
       </Panel>
       <!-- The action bar (F3 needs-you spec §2, §5): fixed under the card, outside it, always in view. A and D reach the same methods as its buttons. -->
       <Border x:Name="NeedsYouActionBar" Grid.Row="1" Classes="action-bar" IsVisible="{Binding Bar.HasBar}" AutomationProperties.AccessibilityView="Control" AutomationProperties.ControlTypeOverride="Group" AutomationProperties.Name="{Binding Bar.BarName}">
        <StackPanel MaxWidth="760" Spacing="8">
          <WrapPanel x:Name="NeedsYouBarButtons" Orientation="Horizontal" ItemSpacing="8" LineSpacing="6" IsVisible="{Binding Bar.ShowsActions}">
            <Button x:Name="NeedsYouApprove" Classes="bar-button primary" Classes.asking="{Binding Bar.IsAsking}" IsVisible="{Binding Bar.ShowsPrimary}" IsEnabled="{Binding Bar.PrimaryEnabled}"
                    Click="OnApprove" AutomationProperties.Name="{Binding Bar.ApproveName}">
              <StackPanel Orientation="Horizontal" Spacing="8" AutomationProperties.AccessibilityView="Raw">
                <TextBlock Text="{Binding Bar.ApproveWord}" VerticalAlignment="Center" />
                <Border Classes="kbd"><TextBlock Classes="kbd" Text="A" /></Border>
              </StackPanel>
            </Button>
            <Button x:Name="NeedsYouTerminal" Classes="bar-button terminal" IsVisible="{Binding Bar.ShowsTerminal}" Click="OnTerminal" AutomationProperties.Name="{Binding Bar.TerminalName}"
                    ToolTip.Tip="{Binding Bar.TerminalTip}">
              <StackPanel Orientation="Horizontal" Spacing="6" AutomationProperties.AccessibilityView="Raw">
                <TextBlock Text="⌂" VerticalAlignment="Center" />
                <TextBlock Classes="mono" Text="{Binding Bar.TerminalButton}" VerticalAlignment="Center" />
              </StackPanel>
            </Button>
            <Button x:Name="NeedsYouSendBack" Classes="bar-button" IsVisible="{Binding Bar.ShowsSendBack}" IsEnabled="{Binding Bar.SendBackEnabled}" Click="OnSendBack"
                    ToolTip.Tip="{Binding Bar.SendBackTooltip}" ToolTip.ShowOnDisabled="True" AutomationProperties.Name="{Binding Bar.SendBackName}" AutomationProperties.HelpText="{Binding Bar.SendBackTooltip}">
              <StackPanel Orientation="Horizontal" Spacing="8" AutomationProperties.AccessibilityView="Raw">
                <TextBlock Text="{Binding Bar.SendBackWord}" VerticalAlignment="Center" />
                <Border Classes="kbd"><TextBlock Classes="kbd" Text="D" /></Border>
              </StackPanel>
            </Button>
            <!-- A feature that is not built yet: a dashed chip, not a control (no focus, no key). -->
            <Panel x:Name="NeedsYouEditChip" IsVisible="{Binding Bar.ShowsEditChip}" Focusable="False" AutomationProperties.AccessibilityView="Content">
              <Rectangle Classes="chip-dash" RadiusX="6" RadiusY="6" />
              <StackPanel Orientation="Horizontal" Spacing="6" Margin="10,4">
                <TextBlock Classes="caption muted" Text="{Binding Bar.EditWord}" VerticalAlignment="Center" />
                <TextBlock Classes="caption subtle" Text="{Binding Bar.NotAvailableWord}" VerticalAlignment="Center" />
              </StackPanel>
            </Panel>
          </WrapPanel>
          <StackPanel x:Name="NeedsYouReasonBox" Spacing="6" IsVisible="{Binding Bar.IsSendingBack}">
            <Grid ColumnDefinitions="*,Auto">
              <TextBlock Classes="body-strong" Text="{Binding Bar.ReasonLabel}" />
              <TextBlock Grid.Column="1" Classes="caption subtle" Text="{Binding Bar.ReasonRequired}" />
            </Grid>
            <TextBox x:Name="NeedsYouReason" AcceptsReturn="True" TextWrapping="Wrap" MinHeight="64" MaxHeight="120" Text="{Binding Bar.ReasonText, Mode=TwoWay}" PlaceholderText="{Binding Bar.ReasonPlaceholder}"
                     KeyDown="OnReasonKey" AutomationProperties.Name="{Binding Bar.ReasonName}" />
            <StackPanel Orientation="Horizontal" Spacing="8">
              <Button x:Name="NeedsYouSend" Classes="bar-button primary" IsEnabled="{Binding Bar.CanSend}" Click="OnSend" AutomationProperties.Name="{Binding Bar.SendWord}">
                <StackPanel Orientation="Horizontal" Spacing="8" AutomationProperties.AccessibilityView="Raw">
                  <TextBlock Text="{Binding Bar.SendWord}" VerticalAlignment="Center" />
                  <Border Classes="kbd"><TextBlock Classes="kbd" Text="Ctrl" /></Border>
                  <Border Classes="kbd"><TextBlock Classes="kbd" Text="Enter" /></Border>
                </StackPanel>
              </Button>
              <Button x:Name="NeedsYouCancel" Classes="bar-button" Click="OnCancel" AutomationProperties.Name="{Binding Bar.CancelWord}">
                <StackPanel Orientation="Horizontal" Spacing="8" AutomationProperties.AccessibilityView="Raw">
                  <TextBlock Text="{Binding Bar.CancelWord}" VerticalAlignment="Center" />
                  <Border Classes="kbd"><TextBlock Classes="kbd" Text="Esc" /></Border>
                </StackPanel>
              </Button>
            </StackPanel>
          </StackPanel>
          <StackPanel x:Name="NeedsYouDropBox" Orientation="Horizontal" Spacing="8" IsVisible="{Binding Bar.IsConfirmingDrop}">
            <TextBlock Classes="body" Text="{Binding Bar.DropAsk}" VerticalAlignment="Center" />
            <Button x:Name="NeedsYouDropConfirm" Classes="bar-button asking" Click="OnDropConfirm" AutomationProperties.Name="{Binding Bar.DropConfirmWord}">
              <TextBlock Text="{Binding Bar.DropConfirmWord}" AutomationProperties.AccessibilityView="Raw" />
            </Button>
            <Button x:Name="NeedsYouDropCancel" Classes="bar-button" Click="OnCancel" AutomationProperties.Name="{Binding Bar.CancelWord}">
              <TextBlock Text="{Binding Bar.CancelWord}" AutomationProperties.AccessibilityView="Raw" />
            </Button>
          </StackPanel>
          <TextBlock x:Name="NeedsYouBarRefusal" Classes="caption attention" Text="{Binding Bar.Refusal}" IsVisible="{Binding Bar.HasRefusal}" TextWrapping="Wrap" AutomationProperties.LiveSetting="Polite" />
          <TextBlock x:Name="NeedsYouBarHint" Classes="caption muted" Classes.attention="{Binding Bar.HintIsAttention}" Classes.success="{Binding Bar.HintIsSuccess}" Text="{Binding Bar.Hint}"
                     IsVisible="{Binding Bar.HasHint}" TextWrapping="Wrap" AutomationProperties.LiveSetting="Polite" />
        </StackPanel>
       </Border>
      </Grid>
    </Grid>'''
assert old_close in s
s=s.replace(old_close,new_close)
open(p,'w').write(s)

# key hints: add A, D and Enter after J K
p=root+'Views/NeedsYou/NeedsYouPage.axaml'
s=open(p).read()
old_hints='''          <StackPanel x:Name="NeedsYouKeyHints" Grid.Row="4" Orientation="Horizontal" Spacing="6" Margin="16,8" AutomationProperties.AccessibilityView="Raw">
            <Border Classes="kbd"><TextBlock Classes="kbd" Text="J" /></Border>
            <Border Classes="kbd"><TextBlock Classes="kbd" Text="K" /></Border>
            <TextBlock Classes="caption subtle" Text="{Binding KeyMoveWord}" VerticalAlignment="Center" />
          </StackPanel>'''
new_hints='''          <WrapPanel x:Name="NeedsYouKeyHints" Grid.Row="4" Orientation="Horizontal" ItemSpacing="6" LineSpacing="4" Margin="16,8" AutomationProperties.AccessibilityView="Raw">
            <Border Classes="kbd"><TextBlock Classes="kbd" Text="J" /></Border>
            <Border Classes="kbd"><TextBlock Classes="kbd" Text="K" /></Border>
            <TextBlock Classes="caption subtle" Text="{Binding KeyMoveWord}" VerticalAlignment="Center" />
            <Border Classes="kbd"><TextBlock Classes="kbd" Text="Enter" /></Border>
            <TextBlock Classes="caption subtle" Text="{Binding KeyOpenWord}" VerticalAlignment="Center" />
            <Border Classes="kbd"><TextBlock Classes="kbd" Text="A" /></Border>
            <TextBlock Classes="caption subtle" Text="{Binding KeyApproveWord}" VerticalAlignment="Center" />
            <Border Classes="kbd"><TextBlock Classes="kbd" Text="D" /></Border>
            <TextBlock Classes="caption subtle" Text="{Binding KeySendBackWord}" VerticalAlignment="Center" />
          </WrapPanel>'''
assert old_hints in s
s=s.replace(old_hints,new_hints)
open(p,'w').write(s)

p=root+'Views/NeedsYou/NeedsYouStyles.axaml'
s=open(p).read()
s=s.replace('<Style Selector="Panel.detail-canvas">','<Style Selector="Grid.detail-canvas">')
s=s.replace('''  <!-- The sticky header over''','''  <!-- The action bar (needs-you spec §2, slice 2): a strip under the card on Surface1 with a Hairline on top; buttons 32 px high, Radius.SM; the primary is Primary on OnPrimary,
       "Approve?" is AttentionSoft with an Attention border and text, the terminal button is Surface2 with HairlineStrong in mono. The dashed chip is a Rectangle stroke. -->
  <Style Selector="Border.action-bar">
    <Setter Property="Background" Value="{DynamicResource Brush.Surface1}" />
    <Setter Property="BorderBrush" Value="{DynamicResource Brush.Hairline}" />
    <Setter Property="BorderThickness" Value="0,1,0,0" />
    <Setter Property="Padding" Value="24,10,24,12" />
  </Style>
  <Style Selector="Button.bar-button">
    <Setter Property="MinHeight" Value="32" />
    <Setter Property="Padding" Value="12,0" />
    <Setter Property="CornerRadius" Value="{StaticResource Radius.SM}" />
    <Setter Property="Background" Value="{DynamicResource Brush.Surface2}" />
    <Setter Property="BorderBrush" Value="{DynamicResource Brush.Hairline}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.Ink}" />
  </Style>
  <Style Selector="Button.bar-button.primary">
    <Setter Property="Background" Value="{DynamicResource Brush.Primary}" />
    <Setter Property="BorderBrush" Value="{DynamicResource Brush.Primary}" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.OnPrimary}" />
  </Style>
  <Style Selector="Button.bar-button.primary:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource Brush.PrimaryHover}" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.OnPrimary}" />
  </Style>
  <Style Selector="Button.bar-button.asking, Button.bar-button.primary.asking">
    <Setter Property="Background" Value="{DynamicResource Brush.AttentionSoft}" />
    <Setter Property="BorderBrush" Value="{DynamicResource Brush.Attention}" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.Attention}" />
  </Style>
  <Style Selector="Button.bar-button.asking:pointerover /template/ ContentPresenter#PART_ContentPresenter, Button.bar-button.primary.asking:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource Brush.AttentionSoft}" />
    <Setter Property="Foreground" Value="{DynamicResource Brush.Attention}" />
  </Style>
  <Style Selector="Button.bar-button.terminal">
    <Setter Property="BorderBrush" Value="{DynamicResource Brush.HairlineStrong}" />
  </Style>
  <Style Selector="Button.bar-button:focus-visible">
    <Setter Property="BorderBrush" Value="{DynamicResource Brush.PrimaryHover}" />
  </Style>
  <Style Selector="Rectangle.chip-dash">
    <Setter Property="Stroke" Value="{DynamicResource Brush.HairlineStrong}" />
    <Setter Property="StrokeThickness" Value="1" />
    <Setter Property="StrokeDashArray" Value="3,3" />
    <Setter Property="Fill" Value="Transparent" />
  </Style>
  <Style Selector="TextBlock.success">
    <Setter Property="Foreground" Value="{DynamicResource Brush.Success}" />
  </Style>
  <!-- The sticky header over''',1)
open(p,'w').write(s)
