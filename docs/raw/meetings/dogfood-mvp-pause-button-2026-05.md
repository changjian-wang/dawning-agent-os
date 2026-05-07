# dogfood: MVP 暂停按钮 (2026-05-07 起)

baseline:
- git: a3f96cb
- desktop: 0.0.1

## 2026-05-07 真实使用 1 小时

### 反例
- **空的新会话不能删除**：误点 `+ 新会话` 3 次后 3 个空 pill 杵着没法清。
- **chat 内容不进 memory**：在 chat 输入了 3 个真实观点（什么是知识图谱 / 减少 agent 幻觉 / prompt 自动优化），memory 视图 = 0。心智期待：说出来的有价值的就该沉淀。
- **inbox 在被我当 FAQ 用**：今天捕获的 4 条都是「产品自己是干嘛的」类问题（保存在哪 / 以什么方式 / Capture a thought 是什么 / source 怎么填）。说明 UI 文案不自解释。
- **source 字段不知道填什么**：填空白比填错更可能，但产品没暗示 source 是干什么的。

### 没用上的
- 📒 Save：今天 0 次点击。
- Summarize / Tags：少数几次（具体次数 V0 不落库）。

### 暂停瞬间想到
- 「我刚才聊的这些，下次 chat 还能用吗？」→ 不能（每个 chat session 独立）
- 「source 留空会怎样？」→ 不知道

## 2026-05-07 第 2 次使用感受

### 反例
- **Save 价值不可见**：点 Save 后，"存到 memory 之后有什么作用"完全不知道。
- **Tags 用途不读自明**：按钮在那但不清楚做什么。
- **`不知道按哪个 / 怎么填` 累积**：source 字段（信号 3）+ Tags 用途（这次）= 2 次。

### 主动倾向
- 「memory 应该来自用户的输入，自动生成 memory，用户怎么可能知道组织记忆呢？」
- 「当前这个用起来太复杂了。」
- 倾向方向：D 零按钮（后端自动）/ B 统一收件箱（chat 自动沉淀），均反对当前显式三按钮模型。

### 元认知（产品能力层，非 UI 主叙事）
- 「没接入 RAG / ReAct，只能问一句答一句」→ 即使 memory 攒下了也不会被 chat 复用 → Save 没有回报闭环。

## 信号 9：质疑 Summary/Tags/Save 三按钮的存在意义

- Save 不应该存在，应自动保存
- summary 和 tags 不知道为了哪个未来功能

agent 解读（参 ADR-030 §背景 + ADR-031 §背景 + ADR-034）：
- Summary/Tags 是 ADR-014 四步骤 pipeline 的中间积木，下游消费（classify / curation options / 兴趣画像 ADR-013）都未建
- Save 把 inbox 项晋升为 memory，但 chat 不读 memory（路径 β 要补的根因），所以晋升后无回报
- 我判断 Save 该不该存在，要在路径 β 闭环建起来后用数据回答，不是现在