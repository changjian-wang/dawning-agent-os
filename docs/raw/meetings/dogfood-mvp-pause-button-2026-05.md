## 2026-05-07 第 3 次使用感受（ADR-038 chat-context-memory-injection-v0 上线后）

baseline:
- git: 15a9dca（ADR-038 5 个 commit 全部完成）
- desktop: 0.0.1
- session: dogfood 第 2 次使用感受之后约 2 小时，5 commit 落地 + 全测试 436/436 通过

### 实验脚本（agent 给的最短可验证脚本）

按 4 步跑：
1. 输入「怎么保存下来比较好？」（设计为命中 ledger 中既有 memory「以什么方式保存下来？」）
2. 输入「今天天气怎么样？」（设计为 0 命中，验证 F1 静默）
3. 通过右侧 Capture 输入「我目前在用 .NET 10 + Electron 开发桌面应用 dawning-agent-os」→ 点 Save 晋升 memory
4. 新会话输入「我用什么技术栈开发桌面应用？」（设计为命中 step 3 刚 Save 的 memory）

### 实测结果

- step 1：✅ 出现 `📒 引用了 1 条 memory`，可展开看到 `019e0145 · 以什么方式保存下来？`
- step 2：✅ 完全无 memory 小字
- step 3 误投：把「我目前在用 .NET 10 + Electron 开发桌面应用 dawning-agent-os」打到了 chat 输入框（设计上要进 capture），LLM 答了一通无意义的"用户设置/应用日志/临时缓存"建议
- step 3 重做：在右侧 Capture 输入同样内容 → Save → 出现 inline 小字 `✓ saved as memory 019e01cd (source=InboxAction, scope=inbox)`
- step 4：✅✅ `📒 引用了 2 条 memory`；LLM 实际答案 = `您目前使用的技术栈是 .NET 10 + Electron`，**首次出现"LLM 真的从注入的 system prompt 末尾读到事实并据此回答"**——PURPOSE.md MVP 第一信号「Memory 真实复用」首次发生

### 用户原话

- 第一次试用（step 1 之前）：「我试了下，没看出来有什么实质性的功能变化」——后被 agent 解释为：当时 ledger 只有 1 条 memory，且 chat 提的是知识图谱话题，朴素 bigram 0 命中，符合 F1 静默；但「无声」被误读为「功能没生效」
- step 4 之后：（验证脚本走完后）符合脚本预期

### 信号 9：朴素 LIKE 检索把"提问"当事实注入

- step 4 命中的 2 条 memory 中，其中一条是 ledger 里既有的「以什么方式保存下来？」——它本身是**未答的提问**，不是事实
- 命中原因：查询「我用什么技术栈开发桌面应用？」的 bigram 包含「什么」，与那条 memory 共享 `什么` bigram → SQL `LIKE '%什么%'` 命中
- ADR-038 §A1 朴素关键词不区分「事实 / 提问 / 偏好 / 任务」；最坏情况是注入污染上下文，让 LLM 基于一个未答的问题误推
- 命中 ADR-038 §`adr_revisit_when` 第 3 条「用户开始抱怨『引用了过时 / 错误的 memory』」的早期形态（暂未抱怨，但事实已发生）

### 信号 10：双输入框心智模型负担延续（ADR-035 dogfood 第 1 天信号 10 的复发）

- step 3 把"想沉淀的内容"误投到 chat 输入框
- 用户脑子里没有「左 chat 写想问的 / 右 capture 写想沉淀的」这层切分
- ADR-035 信号 10 是会话切换器形态，本次信号 10 是**输入框分工**——两条都是 UI 主叙事的边缘事故，但 ADR-038 §决策 D2 + ADR-035 §D4「不动主屏」共同要求新方案不能新增主屏按钮 / 视图

### 信号 11：Save 反馈闭环已部分修复（确认型，非新需求）

- 点 Save 后，inbox item 上出现 `Saved ✓ — save again` 状态切换
- 同步出现一行 inline 反馈：`✓ saved as memory 019e01cd (source=InboxAction, scope=inbox)`
- 这条反馈不在 ADR-038 范围内，应该是更早的某次提交带的（待查 git log）；但它**部分修复**了 dogfood 第 2 天信号 4「Save 没回报闭环」
- 仍未解决的部分：用户看不到「这条 memory 下次会被哪些 chat 召回」——可考虑用 `ChatMemoryRetriever.Tokenize` 抽 3 个关键词作为预览，但要先评估是否暴露内部实现细节

### 元认知

- ADR-038 5-commit MVP 端到端打通，PURPOSE.md MVP 第一信号「Memory 真实复用」首次出现
- 但首次成功也立刻暴露了 3 条新信号；这与 ADR-035 的 dogfood 节奏一致（ADR-035 上线第 1 天就出 9~11 条信号）
- 下一个 ADR 候选（plan-first，未起草）：信号 9 → memory kind 过滤；信号 10 → chat 内沉淀入口；信号 11 → Save 反馈带关键词预览
- 信号 9 优先级最高（污染 LLM 上下文，跟检索质量直接挂钩）


### 信号 12：promote-content gap（capture 问题，不 capture 答案）

- 触发：ADR-039 dogfood 验证 4「保存方式怎么选？」LLM 给出完全通用的「数据类型 / 使用场景 / 安全性 / 可扩展性」清单，**0 条 memory 召回**
- 排查：ledger 中关于"保存"主题命中的全部是问句条目（`以什么方式保存下来？` / `最终会保存在哪里？` / `这些想法和灵感最终会沉淀到哪里呢？`），全部被 ADR-039 §B1 静默过滤；**没有任何 fact 条目**记录"我决定用 SQLite + 软删 + scope 字段"这种产品决策结果
- 根因：用户的 inbox 习惯是 capture **问题**（"我想到一个待解的事"），不是 capture **答案**（"这事我决定这么办了"）。memory 沉淀路径「问题 → 思考 → 答案 → fact」中**最后一步未发生**
- 与 ADR-039 的关系：ADR-039 修的是「question 不该作为 fact 注入」（retrieve 端），但本信号暴露 ledger 写入端**事实条目供给不足**——即使 retrieve 完美，ledger 没有事实可召回。两个问题正交，ADR-039 不会被本信号 invalidate
- 与 PURPOSE.md MVP 信号 #2「LLM 真的有记忆般的回应」的关系：信号 #1 已在 ADR-038 + ADR-039 上线后稳定可重现；信号 #2 受本信号制约——没有事实积累，"记忆般的回应"只能停留在「我用 .NET 10 + Electron」这种用户已显式 Save 的少数事实上
- 与 dogfood 信号 10「双输入框心智」的关系：信号 10 在 chat 主屏抱怨"想沉淀 chat 里说出来的东西时不知道往哪写"——本信号是同一裂缝的另一面（用户**有**沉淀意图但缺**入口**）。两个信号联合证据强度足以驱动 ADR-040 候选「chat 内沉淀入口 v0」
- 命中 [ADR-035](../../pages/adrs/desktop-mvp-ux-thesis-via-dogfood.md) §dogfood 第 5 信号触发条件之一：用户开始抱怨「我说过的事没被记住」（早期形态——不是抱怨记忆，是抱怨**没有事实可记忆**）
- 时间线：dogfood 第 2 天，ADR-039 上线后 ~10 分钟（同一次 dogfood session）
- 优先级：高于信号 11（Save 反馈完善已部分修复），低于信号 10（信号 10 是"用户主动想沉淀但找不到入口"，比"用户没意识到要沉淀"更紧迫；但信号 12 给信号 10 提供了客观证据强化）

### 后续 ADR 候选刷新（替换前一节末尾）

- 信号 10 + 信号 12 联合：ADR-040 候选「chat 内沉淀入口 v0」——在 chat 消息上加 "沉淀这条进 memory" 按钮，让用户能把 LLM 给出的答案 / 自己说出来的产品决策直接 capture 成 ledger 条目（绕过 inbox→Save 路径）
- 信号 11：Save 反馈带 token 预览的剩余部分（先翻 git log 确认已修复程度，再决定要不要起 ADR）
- 信号 9：✅ 已被 ADR-039 关闭（commit 07fc3d1，dogfood 第 2 天验证 1+2+4 通过）